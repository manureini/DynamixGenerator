using NHibernate.Cfg;
using NHibernate.Mapping;
using NHibernate.Tool.hbm2ddl;
using NHibernate.Util;
using System;
using System.Linq;

namespace DynamixGenerator.NHibernate
{
    public class DynamixSchemaUpdater
    {
        public static void UpdateSchema(Configuration pConfiguration, DynamixClass[] pClasses)
        {
            //first all classes and then all properties, because property could reference dynamix class
            foreach (var dynClass in pClasses)
            {
                AddClass(pConfiguration, dynClass);
            }

            foreach (var dynClass in pClasses)
            {
                if (dynClass.Properties != null)
                    foreach (var property in dynClass.Properties)
                    {
                        if (!property.IsOneToMany)
                            AddProperty(pConfiguration, property);
                    }
            }

            foreach (var dynClass in pClasses)
            {
                if (dynClass.Properties != null)
                    foreach (var property in dynClass.Properties)
                    {
                        if (property.IsOneToMany)
                            AddProperty(pConfiguration, property);
                    }
            }

            RunSchemaUpdate(pConfiguration);
        }

        private static void AddClass(Configuration pConfiguration, DynamixClass pDynClass)
        {
            var mapping = pConfiguration.CreateMappings();

            var existing = mapping.GetClass(pDynClass.FullName);

            if (existing == null)
            {
                Column columnId = new Column("Id")
                {
                    IsNullable = false
                };

                Table table = mapping.AddTable(null, null, "_" + pDynClass.Name, null, false, "all"); //isabstract is false ?

                table.AddColumn(columnId);

                var valueId = new SimpleValue()
                {
                    IdentifierGeneratorStrategy = "assigned",
                    TypeName = "guid",
                    Table = table,
                    NullValue = "undefined"
                };

                valueId.AddColumn(columnId);

                var idProperty = new Property(valueId)
                {
                    Name = "Id",
                    Value = valueId,
                };

                var targetClass = new RootClass()
                {
                    EntityName = pDynClass.FullName,
                    DiscriminatorValue = pDynClass.FullName,
                    IdentifierProperty = idProperty,
                    Identifier = valueId,
                    IsMutable = true,
                    ClassName = pDynClass.GetTypeReference().AssemblyQualifiedName
                };

                ((ITableOwner)targetClass).Table = table;

                PrimaryKey pk = new PrimaryKey()
                {
                    Name = "Id",
                    Table = table,
                };
                pk.AddColumn(columnId);
                table.PrimaryKey = pk;

                mapping.AddClass(targetClass);
            }
            else
            {
                existing.ClassName = pDynClass.GetTypeReference().AssemblyQualifiedName;
            }
        }

        private static void AddProperty(Configuration pConfiguration, DynamixProperty pDynProperty)
        {
            var mapping = pConfiguration.CreateMappings();

            var table = mapping.IterateTables.First(t => t.Name == "_" + pDynProperty.DynamixClass.Name);

            var persistentClass = pConfiguration.ClassMappings.First(c => c.EntityName == pDynProperty.DynamixClass.FullName);

            if (pDynProperty.IsReference)
            {
                var columnName = pDynProperty.Name + "Id";

                if (persistentClass.Table.ColumnIterator.Any(c => c.Name == columnName))
                    return;

                PersistentClass referencedPersistentClass = pConfiguration.GetClassMapping(pDynProperty.GetFullTypeName());

                IValue relation;

                if (pDynProperty.IsOneToMany)
                {
                    string roleName = persistentClass.EntityName + "." + pDynProperty.Name;

                    if (mapping.IterateCollections.Any(c => c.Role == roleName))
                        return;

                    var keyName = pDynProperty.DynamixClass.Name + "Id";

                    var foreignColumn = referencedPersistentClass.Table.ColumnIterator.First(c => c.Name == keyName);

                    DependantValue valueKey = new DependantValue(referencedPersistentClass.Table, referencedPersistentClass.Identifier);

                    valueKey.AddColumn(foreignColumn);

                    var elementType = pDynProperty.Type.GetGenericArguments()[0];

                    var set = new Set(persistentClass)
                    {
                        Key = valueKey,
                        Element = new OneToMany(persistentClass)
                        {
                            AssociatedClass = referencedPersistentClass,
                            ReferencedEntityName = referencedPersistentClass.EntityName,
                        },

                        CollectionTable = referencedPersistentClass.Table,
                        GenericArguments = new Type[] { elementType },
                        CacheRegionName = persistentClass.EntityName + "." + pDynProperty.Name,
                        IsGeneric = true,
                        Role = roleName
                    };

                    mapping.AddCollection(set);
                    relation = set;
                }
                else
                {
                    var manyToOne = new ManyToOne(referencedPersistentClass.Table)
                    {
                        PropertyName = pDynProperty.Name,
                        ReferencedEntityName = referencedPersistentClass.EntityName,
                        IsLazy = false,
                    };

                    var propColumn = new Column(columnName)
                    {
                        IsUnique = pDynProperty.IsUnique
                    };
                    manyToOne.AddColumn(propColumn);

                    persistentClass.Table.AddColumn(propColumn);
                    persistentClass.Table.CreateForeignKey(null, new[] { propColumn }, referencedPersistentClass.EntityName);

                    relation = manyToOne;
                }

                var prop = new Property
                {
                    Value = relation,
                    Name = pDynProperty.Name,
                    //     PropertyAccessorName = "property"
                };

                persistentClass.AddProperty(prop);
            }
            else
            {
                if (persistentClass.Table.ColumnIterator.Any(c => c.Name == pDynProperty.Name))
                    return;

                Column column = new Column(pDynProperty.Name);

                table.AddColumn(column);

                var valueName = new SimpleValue(table)
                {
                    TypeName = pDynProperty.Type.AssemblyQualifiedName,
                };

                valueName.AddColumn(column);

                persistentClass.AddProperty(new Property()
                {
                    Name = pDynProperty.Name,
                    Value = valueName,
                });
            }
        }

        private static void RunSchemaUpdate(Configuration cfg)
        {
            var update = new SchemaUpdate(cfg);
            update.Execute(true, true);

            if (update.Exceptions.Count > 0)
            {
                foreach (var exception in update.Exceptions)
                {
                    Console.WriteLine(exception);
                }

                throw update.Exceptions[0];
            }
        }
    }
}
