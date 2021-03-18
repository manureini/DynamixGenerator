using NHibernate.Cfg;
using NHibernate.Mapping;
using NHibernate.Tool.hbm2ddl;
using NHibernate.Type;
using NHibernate.Util;
using System;
using System.Linq;
using System.Reflection;

namespace DynamixGenerator.NHibernate
{
    public class DynamixSchemaUpdater
    {
        private static readonly FieldInfo EntityTypeReturnedClassField = typeof(EntityType).GetField("returnedClass", BindingFlags.Instance | BindingFlags.NonPublic);

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
                        if (!property.IsReference)
                            AddProperty(pConfiguration, property);
                    }
            }

            RunSchemaUpdate(pConfiguration);

            foreach (var dynClass in pClasses)
            {
                if (dynClass.Properties != null)
                    foreach (var property in dynClass.Properties)
                    {
                        if (property.IsReference)
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

            var existing = persistentClass.PropertyIterator.FirstOrDefault(c => c.Name == pDynProperty.Name);

            if (pDynProperty.IsReference)
            {
                PersistentClass referencedPersistentClass = pConfiguration.GetClassMapping(pDynProperty.Type.FullName);

                IValue relation;

                if (pDynProperty.IsOneToMany)
                {
                    string roleName = persistentClass.EntityName + "." + pDynProperty.Name;

                    var existingCollection = mapping.IterateCollections.FirstOrDefault(c => c.Role == roleName);

                    if (existingCollection != null)
                    {
                        existingCollection.GenericArguments = new Type[] { pDynProperty.Type };
                        return;
                    }

                    var keyName = pDynProperty.DynamixClass.Name + "Id";

                    var foreignColumn = referencedPersistentClass.Table.ColumnIterator.First(c => c.Name == keyName);

                    DependantValue valueKey = new DependantValue(referencedPersistentClass.Table, referencedPersistentClass.Identifier);

                    valueKey.AddColumn(foreignColumn);

                    var set = new Set(persistentClass)
                    {
                        Key = valueKey,
                        Element = new OneToMany(persistentClass)
                        {
                            AssociatedClass = referencedPersistentClass,
                            ReferencedEntityName = referencedPersistentClass.EntityName,
                        },

                        CollectionTable = referencedPersistentClass.Table,
                        GenericArguments = new Type[] { pDynProperty.Type },
                        IsGeneric = true,
                        Role = roleName,
                        IsLazy = false //TODO?
                    };

                    mapping.AddCollection(set);
                    relation = set;
                }
                else
                {
                    if (existing != null)
                    {
                        //Hibernate stores the old reference. We'll update it ;)
                        EntityTypeReturnedClassField.SetValue(existing.Type, pDynProperty.Type);
                        return;
                    }

                    var manyToOne = new ManyToOne(persistentClass.Table)
                    {
                        PropertyName = pDynProperty.Name,
                        ReferencedEntityName = referencedPersistentClass.EntityName,
                        FetchMode = global::NHibernate.FetchMode.Join,
                        IsLazy = false,
                        ReferencedPropertyName = string.IsNullOrWhiteSpace(pDynProperty.ReferencedPropertyName) ? null : pDynProperty.ReferencedPropertyName,
                    };

                    if (pDynProperty.Formula == null)
                    {
                        var propColumn = new Column(pDynProperty.Name + "Id")
                        {
                            IsUnique = pDynProperty.IsUnique,
                        };
                        manyToOne.AddColumn(propColumn);

                        persistentClass.Table.AddColumn(propColumn);
                        persistentClass.Table.CreateForeignKey(null, new[] { propColumn }, referencedPersistentClass.EntityName);
                    }
                    else
                    {
                        manyToOne.AddFormula(new Formula()
                        {
                            FormulaString = pDynProperty.Formula
                        });
                    }

                    relation = manyToOne;
                }

                var prop = new Property
                {
                    Value = relation,
                    Name = pDynProperty.Name,
                };

                persistentClass.AddProperty(prop);
            }
            else
            {
                if (existing != null)
                {
                    if (pDynProperty.Formula == null)
                    {
                        var sval = (SimpleValue)existing.Value;
                        //TODO Update Formula
                    }
                    return;
                }

                var value = new SimpleValue(table)
                {
                    TypeName = pDynProperty.Type.AssemblyQualifiedName,
                };

                if (pDynProperty.Formula == null)
                {
                    var column = new Column(pDynProperty.Name);
                    table.AddColumn(column);

                    value.AddColumn(column);
                }
                else
                {
                    value.AddFormula(new Formula()
                    {
                        FormulaString = pDynProperty.Formula
                    });
                }

                persistentClass.AddProperty(new Property()
                {
                    Name = pDynProperty.Name,
                    Value = value,
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
