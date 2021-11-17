using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DynamixGenerator
{
    public class DynamixService
    {
        public IDynamixStorage Storage { get; protected set; }

        public DynamixClass[] LoadedClasses { get; protected set; }

        public string LastAssemblyFullName { get; protected set; }

        public string AssemblyNamePrefix { get; set; } = "DynamicGenerated_";

        public string AssemblyFileName { get; set; }

        protected long mGeneratedAssemblyCount = 0;

        public DynamixService(IDynamixStorage pStorage)
        {
            Storage = pStorage;
        }

        public (Assembly, byte[] bytes) CreateAndLoadAssembly()
        {
            var classes = Storage.GetDynamixClasses();

            byte[] assembly;

            if (AssemblyFileName != null && File.Exists(AssemblyFileName))
            {
                assembly = File.ReadAllBytes(AssemblyFileName);
            }
            else
            {
                var assemblyName = AssemblyNamePrefix + mGeneratedAssemblyCount;
                mGeneratedAssemblyCount++;

                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == assemblyName))
                    throw new Exception($"Assembly with name {assemblyName} already loaded");

                var code = DynamixGenerator.GenerateCode(assemblyName, classes);

                DynamixCompiler compiler = new DynamixCompiler(assemblyName);
                assembly = compiler.CompileCode(code);
            }

            if (AssemblyFileName != null && !File.Exists(AssemblyFileName))
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
            LastAssemblyFullName = asm.FullName;

            return (asm, assembly);
        }
    }
}
