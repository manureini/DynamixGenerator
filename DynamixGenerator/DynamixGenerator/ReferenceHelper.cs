using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamixGenerator
{
    // Important !!!!
    // This class is used in Project Coreflow and DynamixGenerator
    // Update it on all projects for any changes

    public class ReferenceHelper
    {
        protected string mDotnetRootPath;
        protected string mRefRootPath;
        protected Dictionary<string, string[]> mRefDllFiles = new();
        protected Dictionary<string, MetadataReference> mReferenceCache = new Dictionary<string, MetadataReference>();
        protected object mLocker = new object();
        protected SemaphoreSlim mLoadLocker = new SemaphoreSlim(1, 1);

        public ReferenceHelper()
        {
            if (RuntimeInformation.OSArchitecture == Architecture.Wasm)
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mDotnetRootPath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "dotnet") + Path.DirectorySeparatorChar;
            }
            else
            {
                mDotnetRootPath = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            }

            mRefRootPath = Path.Combine(mDotnetRootPath, "packs") + Path.DirectorySeparatorChar;

            if (Directory.Exists(mDotnetRootPath))
            {
                mRefDllFiles = Directory.GetFiles(mRefRootPath, "*.dll", SearchOption.AllDirectories).GroupBy(f => Path.GetFileName(f)).ToDictionary(g => g.Key, x => x.ToArray());
            }
        }

        public async Task LoadReferencesFromWebAsync(HttpClient pHttpClient, Func<Assembly, bool> pFilter = null)
        {
            if (RuntimeInformation.OSArchitecture == Architecture.Wasm)
            {
                try
                {
                    await mLoadLocker.WaitAsync();

                    var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);

                    foreach (var assembly in assemblies)
                    {
                        if (mReferenceCache.ContainsKey(assembly.FullName))
                            continue;

                        if (pFilter != null && pFilter(assembly))
                            continue;

                        var assemblyName = assembly.GetName().Name;

                        var fileName = assemblyName + ".dll";

                        try
                        {
                            var stream = await pHttpClient.GetStreamAsync("/_framework/" + WebUtility.UrlEncode(fileName));
                            mReferenceCache.Add(assembly.FullName, MetadataReference.CreateFromStream(stream));
                        }
                        catch (Exception e)
                        {
                            mReferenceCache.Add(assembly.FullName, null);
                            Console.WriteLine(e);
                        }
                    }
                }
                finally
                {
                    mLoadLocker.Release();
                }
            }
        }

        public void AddAssembly(string pFullName, Stream stream)
        {
            mReferenceCache.Remove(pFullName);
            mReferenceCache.Add(pFullName, MetadataReference.CreateFromStream(stream));
        }

        public void RemoveAssembly(string pFullName)
        {
            mReferenceCache.Remove(pFullName);
        }

        private string FindReferenceAssemblyIfNeeded(string pRuntimeAssembly)
        {
            if (!pRuntimeAssembly.StartsWith(mDotnetRootPath))
                return pRuntimeAssembly;

            if (pRuntimeAssembly.Contains(".Private."))
                return null;

            var runtimeasm = AssemblyName.GetAssemblyName(pRuntimeAssembly);

            string dllFileName = Path.GetFileName(pRuntimeAssembly);

            if (!mRefDllFiles.ContainsKey(dllFileName))
                return null;

            var refFiles = mRefDllFiles[dllFileName].Where(f =>
            {
                var refAsm = AssemblyName.GetAssemblyName(f);

                if (runtimeasm.Version != refAsm.Version)
                    return false;

                if (Encoding.UTF8.GetString(runtimeasm.GetPublicKey()) != Encoding.UTF8.GetString(refAsm.GetPublicKey()))
                    return false;

                return true;
            }).ToArray();

            if (refFiles.Count() <= 1)
                return refFiles.FirstOrDefault();

            Console.WriteLine($"WARNING: Search for referenced assembly {dllFileName} in {mRefRootPath} has mutiple results");

            var runtimePaths = pRuntimeAssembly.Split(Path.DirectorySeparatorChar).ToList();
            var refPaths = runtimePaths.Select(r => r + ".Ref").ToArray();
            runtimePaths.AddRange(refPaths);

            string refPath = refFiles.Select(f => (f, f.Split(Path.DirectorySeparatorChar).Intersect(runtimePaths).Count())).OrderByDescending(f => f.Item2).First().f;

            if (refPath != null && File.Exists(refPath))
            {
                return refPath;
            }

            return pRuntimeAssembly;
        }

        public IEnumerable<MetadataReference> GetMetadataReferences(Func<Assembly, bool> pFilter = null)
        {
            lock (mLocker)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);

                if (RuntimeInformation.OSArchitecture == Architecture.Wasm)
                {
                    var ret = assemblies.Select(a =>
                    {
                        if (mReferenceCache.ContainsKey(a.FullName))
                            return mReferenceCache[a.FullName];

                        return null;
                    }).Where(a => a != null).Distinct().ToArray();

                    return ret;
                }

                return assemblies.Select(a =>
                {
                    try
                    {
                        if (mReferenceCache.ContainsKey(a.FullName))
                            return mReferenceCache[a.FullName];

                        if (pFilter != null && pFilter(a))
                            return null;

                        string location = a.Location;

                        if (location == string.Empty)
                            return null;

                        string referenceAssembly = FindReferenceAssemblyIfNeeded(location);

                        if (referenceAssembly == null)
                            return null;

                        var reference = MetadataReference.CreateFromFile(referenceAssembly);

                        mReferenceCache.Add(a.FullName, reference);

                        return reference;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    return null;
                }).Where(a => a != null).Distinct().ToArray(); //make ToArray here because of lock
            }
        }
    }
}
