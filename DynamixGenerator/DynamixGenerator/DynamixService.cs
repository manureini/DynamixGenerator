using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DynamixGenerator
{
    public class DynamixService
    {
        public IDynamixStorage Storage { get; protected set; }

        public DynamixService(IDynamixStorage pStorage)
        {
            Storage = pStorage;
        }

        public Assembly Initialize(string pAssemblyName)
        {
            var classes = Storage.GetDynamixClasses();
            var code = DynamixGenerator.GenerateCode(classes);

            DynamixCompiler compiler = new DynamixCompiler(pAssemblyName);
            byte[] assembly = compiler.CompileCode(code);

            return AppDomain.CurrentDomain.Load(assembly);
        }

        public void AddClass(DynamixClass pClass)
        {
            Storage.UpdateDynamixClass(pClass);
        }
    }
}
