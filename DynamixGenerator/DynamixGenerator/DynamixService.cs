using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DynamixGenerator
{
    public class DynamixService
    {
        public IDynamixStorage Storage { get; protected set; }

        public DynamixClass[] LoadedClasses;

        protected long mGeneratedAssemblyCount = 0;

        public string AssemblyFileName = null;

        public DynamixService(IDynamixStorage pStorage)
        {
            Storage = pStorage;
        }

        public Assembly CreateAndLoadAssembly(string pAssemblyName = null)
        {
            var classes = Storage.GetDynamixClasses();

            byte[] assembly;

            if (AssemblyFileName == null)
            {
                if (pAssemblyName == null)
                {
                    pAssemblyName = "DynamixGenerated_" + mGeneratedAssemblyCount;
                    mGeneratedAssemblyCount++;
                }

                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == pAssemblyName))
                    throw new Exception($"Assembly with name {pAssemblyName} already loaded");

                var code = DynamixGenerator.GenerateCode(pAssemblyName, classes);

                DynamixCompiler compiler = new DynamixCompiler(pAssemblyName);
                assembly = compiler.CompileCode(code);
            }
            else
            {
                assembly = File.ReadAllBytes(AssemblyFileName);
            }

            if (AssemblyFileName != null)
                File.WriteAllBytes(AssemblyFileName, assembly);

            Assembly asm = null;

            if (AssemblyFileName != null)
            {
                asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == Path.GetFileNameWithoutExtension(AssemblyFileName));
            }

            if (asm == null)
            {
                asm = AppDomain.CurrentDomain.Load(assembly);
            }

            foreach (var dynClass in classes)
            {
                dynClass.UpdateTypeReference(asm);
            }

            LoadedClasses = classes.ToArray();
            return asm;
        }
    }
}
