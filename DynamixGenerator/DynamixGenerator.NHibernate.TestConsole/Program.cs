using NHibernate;
using NHibernate.Cfg;
using NHibernate.Tool.hbm2ddl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace DynamixGenerator.NHibernate.TestConsole
{
    class Program
    {

        protected static void FillStorage(ISession pSession)
        {
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
                IsReference = true,
                IsUnique = true
            });

            classPerson.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "Adresses",
                Type = classAddress.GetTypeReference(),
                DynamixClass = classPerson,
                IsReference = true,
                IsOneToMany = true
            });

            pSession.SaveOrUpdate(classPerson);
            pSession.SaveOrUpdate(classAddress);
        }

        static void Main(string[] args)
        {
            Configuration cfg = new Configuration();
            cfg.Configure(@"hibernate.cfg.xml");
            cfg.AddAssembly(typeof(NHibernateDynamixStorage).Assembly);

            var sessionFactory = cfg.BuildSessionFactory();
            var session = sessionFactory.OpenSession();

            SchemaUpdate schemaUpdate = new SchemaUpdate(cfg);
            schemaUpdate.Execute(true, true);

            //   FillStorage(session);
            session.Flush();

            var storage = new NHibernateDynamixStorage(session);

            DynamixService service = new DynamixService(storage);

            var asm = service.CreateAndLoadAssembly("Dynamic1");

            DynamixSchemaUpdater.UpdateSchema(cfg, service.LoadedClasses);

            sessionFactory = cfg.BuildSessionFactory();
            session = sessionFactory.OpenSession();

            DynamixClass dynPersonClass = service.LoadedClasses[0];
            var personType = dynPersonClass.GetTypeReference();

            var mi = session.GetType().GetMethods().Single(m => m.Name == nameof(session.Query) && m.GetParameters().Length == 0).MakeGenericMethod(personType);
            var query = (IQueryable<object>)mi.Invoke(session, null);

            var persons = query.ToArray();
            Console.WriteLine(persons[0].GetType().AssemblyQualifiedName);

            dynPersonClass.Properties.Add(new DynamixProperty()
            {
                Id = Guid.NewGuid(),
                Name = "Title",
                Type = typeof(int),
                DynamixClass = dynPersonClass
            });

            storage.UpdateDynamixClass(dynPersonClass);
            session.Flush();
            session.Clear();

            var asm2 = service.CreateAndLoadAssembly("Dynamic2");

            DynamixSchemaUpdater.UpdateSchema(cfg, service.LoadedClasses);

            sessionFactory = cfg.BuildSessionFactory();
            session = sessionFactory.OpenSession();

            dynPersonClass = service.LoadedClasses[0];
            personType = dynPersonClass.GetTypeReference();

            mi = session.GetType().GetMethods().Single(m => m.Name == nameof(session.Query) && m.GetParameters().Length == 0).MakeGenericMethod(personType);
            query = (IQueryable<object>)mi.Invoke(session, null);

            persons = query.ToArray();
            Console.WriteLine(persons[0].GetType().AssemblyQualifiedName);
        }
    }
}
