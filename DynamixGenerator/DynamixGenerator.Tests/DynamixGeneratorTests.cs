using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamixGenerator.Tests
{
    [TestClass]
    public class DynamixGeneratorTests
    {
        public IDynamixStorage GetStorage()
        {
            var ret = new MemoryDynamixStorage();

            var classPerson = new DynamixClass()
            {
                Id = Guid.NewGuid(),
                Name = "Person",
                Properties = new List<DynamixProperty>()
            };
            classPerson.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "FirstName",
                Type = typeof(string),
                DynamixClass = classPerson
            });
            classPerson.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "LastName",
                Type = typeof(string),
                DynamixClass = classPerson
            });

            var classAddress = new DynamixClass()
            {
                Id = Guid.NewGuid(),
                Name = "Address",
                Properties = new List<DynamixProperty>()
            };
            classAddress.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "Street",
                Type = typeof(string),
                DynamixClass = classAddress
            });
            classAddress.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "PostCode",
                Type = typeof(int),
                DynamixClass = classAddress
            });
            classAddress.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "Person",
                Type = classPerson.GetTypeReference(),
                DynamixClass = classAddress,
                IsReference = true
            });

            ret.Add(classPerson);
            ret.Add(classAddress);

            return ret;
        }

        [TestMethod]
        public void TestCodeGenerator()
        {
            /*
            var code = DynamixGenerator.GenerateCode("Dynamic", GetStorage().GetDynamixClasses());

            Assert.IsTrue(code.Contains("global::System.String LastName { get; set; }"));
            Assert.IsTrue(code.Contains("global::Dynamic.Person Person { get; set; }"));
            Assert.IsTrue(code.Contains("public class Person"));
            */
        }

        [TestMethod]
        public void TestCompileCodeAndLoad()
        {
            /*
            var storage = GetStorage();
            DynamixService ds = new DynamixService(storage);
            var asm = ds.CreateAndLoadAssembly("DynamicAsm");

            var classPerson = ds.LoadedClasses.First();

            var personType = classPerson.GetTypeReference();
            dynamic person = Activator.CreateInstance(personType);
            person.FirstName = "Hans";
            */
        }
    }
}
