using NHibernate;
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

        private bool mSkipUselessTables;

        public DynamixSchemaUpdater(bool pSkipUselessTables)
        {
            mSkipUselessTables = pSkipUselessTables;
        }


        public void UpdateSchema(Configuration pConfiguration, DynamixClass[] pClasses)
        {
            var mapping = pConfiguration.CreateMappings();

            var classes = pClasses;

            //first all classes and then all properties, because property could reference dynamix class
            foreach (var dynClass in classes)
            {
                AddClass(mapping, dynClass);
            }

            foreach (var dynClass in classes)
            {
                if (dynClass.Properties != null)
                    foreach (var property in dynClass.Properties)
                    {
                        if (!property.IsReference)
                            AddProperty(mapping, property);
                    }
            }

            RunSchemaUpdate(pConfiguration);

            foreach (var dynClass in classes)
            {
                if (dynClass.Properties != null)
                    foreach (var property in dynClass.Properties)
                    {
                        if (property.IsReference)
                            AddProperty(mapping, property);
                    }
            }

            RunSchemaUpdate(pConfiguration);

            MigrateIdsFromBaseToSubTables(pConfiguration, mapping, classes);
        }

        private void MigrateIdsFromBaseToSubTables(Configuration pConfiguration, Mappings mapping, DynamixClass[] classes)
        {
            using var sf = pConfiguration.BuildSessionFactory();
            using var session = sf.OpenSession();

            foreach (var dynClass in classes)
            {
                var persistentClass = mapping.LocatePersistentClassByEntityName(dynClass.FullName);

                if (persistentClass is SingleTableSubclass subClass && subClass.JoinIterator.Any())
                {
                    var table = mapping.IterateTables.Single(t => t.Name == "_" + dynClass.Name).GetQuotedName();

                    if (persistentClass.Discriminator.ColumnSpan != 1)
                        throw new NotSupportedException("Multi Discriminator Columns not supported!");

                    var discriminatorColumn = persistentClass.Discriminator.ColumnIterator.First().Text;

                    var query =
                        @$"DO $$ BEGIN
                            IF NOT EXISTS(SELECT id FROM {table} LIMIT 1) THEN
                                INSERT INTO {table} 
                                    SELECT id FROM {persistentClass.Table.GetQuotedName()} WHERE {discriminatorColumn} = '{persistentClass.DiscriminatorValue}';
                            END IF;
                        END $$;
                        ";

                    session.CreateSQLQuery(query).ExecuteUpdate();
                }
            }

            session.Flush();
        }

        private void AddClass(Mappings pMappings, DynamixClass pDynClass)
        {
            var existing = pMappings.GetClass(pDynClass.FullName);

            if (existing == null)
            {
                Column columnId = new Column("Id")
                {
                    IsNullable = false
                };

                var valueId = new SimpleValue()
                {
                    IdentifierGeneratorStrategy = "assigned",
                    TypeName = "guid",
                    NullValue = "undefined"
                };
                valueId.AddColumn(columnId);

                var idProperty = new Property(valueId)
                {
                    Name = "Id",
                    Value = valueId,
                };

                PersistentClass targetClass = new RootClass()
                {
                    EntityName = pDynClass.FullName,
                    DiscriminatorValue = pDynClass.FullName,
                    IdentifierProperty = idProperty,
                    Identifier = valueId,
                    IsMutable = true,
                    ClassName = pDynClass.GetTypeReference().AssemblyQualifiedName
                };

                bool subTable = string.IsNullOrWhiteSpace(pDynClass.InheritsFrom) || pDynClass.Properties.Any();
                Table table = null;

                if (subTable)
                {
                    table = pMappings.AddTable(null, null, "_" + pDynClass.Name, null, false, "all");
                    table.AddColumn(columnId);

                    PrimaryKey pk = new PrimaryKey()
                    {
                        Name = "Id",
                        Table = table,
                    };
                    pk.AddColumn(columnId);

                    table.PrimaryKey = pk;

                    valueId.Table = table;

                    ((ITableOwner)targetClass).Table = table;
                }

                if (!string.IsNullOrWhiteSpace(pDynClass.InheritsFrom))
                {
                    var superClass = pMappings.GetClass(pDynClass.InheritsFrom);

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

                    if (subTable)
                    {
                        Join join = new Join()
                        {
                            PersistentClass = subclass,
                            Key = dependantKey,
                            Table = table
                        };

                        subclass.AddJoin(join);

                        var foreignKey = table.CreateForeignKey(null, new[] { columnId }, subclass.EntityName);
                        foreignKey.ReferencedTable = superClass.Table;
                    }
                    else
                    {
                        valueId.Table = superClass.Table;
                        ((ITableOwner)targetClass).Table = superClass.Table;
                    }

                    targetClass = subclass;
                }

                pMappings.AddClass(targetClass);
            }
            else
            {
                existing.ClassName = pDynClass.GetTypeReference().AssemblyQualifiedName;
                existing.ProxyInterfaceName = pDynClass.GetTypeReference().AssemblyQualifiedName;
            }
        }

        private void AddProperty(Mappings pMapping, DynamixProperty pDynProperty)
        {
            var persistentClass = pMapping.LocatePersistentClassByEntityName(pDynProperty.DynamixClass.FullName);

            var table = pMapping.IterateTables.SingleOrDefault(t => t.Name == "_" + pDynProperty.DynamixClass.Name);

            var existing = persistentClass.PropertyIterator.FirstOrDefault(c => c.Name == pDynProperty.Name);

            if (pDynProperty.IsReference)
            {
                PersistentClass referencedPersistentClass = pMapping.GetClass(pDynProperty.Type.FullName);

                IValue relation;

                if (pDynProperty.IsOneToMany)
                {
                    string roleName = persistentClass.EntityName + "." + pDynProperty.Name;

                    var existingCollection = pMapping.IterateCollections.FirstOrDefault(c => c.Role == roleName);

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

                    pMapping.AddCollection(set);
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
                        IsIgnoreNotFound = true
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
                    if (subClass.JoinIterator.Any())
                    {
                        var join = subClass.JoinIterator.First();
                        join.AddProperty(property);
                        return;
                    }

                    throw new InvalidOperationException("Not possible code reached");
                }

                persistentClass.AddProperty(property);
            }
        }

        private void RunSchemaUpdate(Configuration cfg)
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
