using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

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
                DynamicClass = classPerson
            });
            classPerson.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "LastName",
                Type = typeof(string),
                DynamicClass = classPerson
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
                DynamicClass = classAddress
            });
            classAddress.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "PostCode",
                Type = typeof(int),
                DynamicClass = classAddress
            });
            classAddress.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "Person",
                Type = classPerson.GetTypeReference(),
                DynamicClass = classAddress
            });


            ret.Add(classPerson);
            ret.Add(classAddress);

            return ret;
        }

        [TestMethod]
        public void TestCodeGenerator()
        {
            var code = DynamixGenerator.GenerateCode("Dynamic", GetStorage().GetDynamixClasses());

            Assert.IsTrue(code.Contains("global::System.String LastName { get; set; }"));
            Assert.IsTrue(code.Contains("global::Dynamic.Person Person { get; set; }"));
            Assert.IsTrue(code.Contains("public class Person"));
        }

        [TestMethod]
        public void TestCompileCodeAndLoad()
        {
            DynamixService ds = new DynamixService(GetStorage());
            var asm = ds.CreateAndLoadAssembly("DynamicAsm");
 
            var personType = asm.GetType("DynamicAsm.Person");
            dynamic person = Activator.CreateInstance(personType);
            person.FirstName = "Hans";
        }
    }
}
