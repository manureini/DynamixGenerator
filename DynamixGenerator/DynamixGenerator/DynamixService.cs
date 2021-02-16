﻿using System;
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

        public Assembly CreateAndLoadAssembly(string pAssemblyName)
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == pAssemblyName))
                throw new Exception($"Assembly with name {pAssemblyName} already loaded");

            var classes = Storage.GetDynamixClasses();
            var code = DynamixGenerator.GenerateCode(pAssemblyName, classes);

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
