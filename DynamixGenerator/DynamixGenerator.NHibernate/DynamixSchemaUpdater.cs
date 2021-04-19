using NHibernate.Cfg;
using NHibernate.Mapping;
using NHibernate.Tool.hbm2ddl;
using NHibernate.Type;
using NHibernate.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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

                Table table = mapping.AddTable(null, null, "_" + pDynClass.Name, null, false, "all");

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

                PrimaryKey pk = new PrimaryKey()
                {
                    Name = "Id",
                    Table = table,
                };
                pk.AddColumn(columnId);
                table.PrimaryKey = pk;

                PersistentClass targetClass = new RootClass()
                {
                    EntityName = pDynClass.FullName,
                    DiscriminatorValue = pDynClass.FullName,
                    IdentifierProperty = idProperty,
                    Identifier = valueId,
                    IsMutable = true,
                    ClassName = pDynClass.GetTypeReference().AssemblyQualifiedName
                };

                ((ITableOwner)targetClass).Table = table;


                if (!string.IsNullOrWhiteSpace(pDynClass.InheritsFrom))
                {
                    var superClass = mapping.GetClass(pDynClass.InheritsFrom);

                    var subclass = new SingleTableSubclass(superClass)
                    {
                        ClassName = pDynClass.GetTypeReference().AssemblyQualifiedName,
                        ProxyInterfaceName = pDynClass.GetTypeReference().AssemblyQualifiedName,
                        DiscriminatorValue = pDynClass.FullName,
                        EntityName = pDynClass.FullName,
                        IsLazy = true,

                    };

                    superClass.AddSubclass(subclass);

                    var dependantKey = new DependantValue(subclass.Table, subclass.Identifier);
                    foreach (Column column in subclass.Key.ColumnIterator)
                    {
                        dependantKey.AddColumn(column);
                    }

                    Join join = new Join()
                    {
                        PersistentClass = subclass,
                        Key = dependantKey,
                        Table = table
                    };

                    subclass.AddJoin(join);

                    targetClass = subclass;

                    var keycolumns = new[] { columnId };

                    var foreignKey = table.CreateForeignKey("fk", keycolumns, subclass.EntityName);
                    foreignKey.ReferencedTable = superClass.Table;

                }

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

            var table = mapping.IterateTables.Single(t => t.Name == "_" + pDynProperty.DynamixClass.Name);

            var persistentClass = pConfiguration.ClassMappings.Single(c => c.EntityName == pDynProperty.DynamixClass.FullName);

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
                    //Update will not work if adding formula later
                    var sval = (SimpleValue)existing.Value;
                    sval.TypeName = pDynProperty.Type.AssemblyQualifiedName;

                    if (!string.IsNullOrWhiteSpace(pDynProperty.Formula) && sval.HasFormula)
                    {
                        var formula = sval.ColumnIterator.First() as Formula;
                        formula.FormulaString = pDynProperty.Formula;
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

                Property property = new Property()
                {
                    Name = pDynProperty.Name,
                    Value = value,
                };

                if (persistentClass is SingleTableSubclass subClass)
                {
                    var join = subClass.JoinIterator.First();
                    join.AddProperty(property);
                    return;
                }

                persistentClass.AddProperty(property);
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
