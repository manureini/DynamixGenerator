using NHibernate;
using NHibernate.Cfg;
using NHibernate.Dialect;
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
        protected HashSet<string> mAlreadyMergedEntities = new();
        protected Dictionary<string, string> mLastCreationSqls = new();

        public string TablePrefix { get; set; } = "_";

        public Configuration UpdateSchema(Func<Configuration> pConfigurationProvider, DynamixClass[] pClasses)
        {
            var cfg = pConfigurationProvider();
            var updateCfg = pConfigurationProvider();
            var updateCfgRef = pConfigurationProvider();

            foreach (var dynClass in pClasses)
            {
                var existing = cfg.GetClassMapping(dynClass.FullName);
                if (existing != null)
                {
                    RemoveFromConfiguration(cfg, existing);
                }

                var existing2 = updateCfg.GetClassMapping(dynClass.FullName);
                if (existing2 != null)
                {
                    RemoveFromConfiguration(updateCfg, existing2);
                }

                var existing3 = updateCfgRef.GetClassMapping(dynClass.FullName);
                if (existing3 != null)
                {
                    RemoveFromConfiguration(updateCfgRef, existing3);
                }
            }

            var mappingCfg = cfg.CreateMappings();
            var mappingUpdate = updateCfg.CreateMappings();
            var mappingUpdateRef = updateCfgRef.CreateMappings();

            //first all classes and then all properties, because property could reference dynamix class
            foreach (var dynClass in pClasses)
            {
                AddClass(mappingCfg, dynClass);
                AddClass(mappingUpdate, dynClass);
                AddClass(mappingUpdateRef, dynClass);
            }

            foreach (var dynClass in pClasses)
            {
                if (dynClass.Properties != null)
                    foreach (var property in dynClass.Properties)
                    {
                        if (!property.IsReference)
                        {
                            AddProperty(mappingCfg, property);
                            AddProperty(mappingUpdate, property);
                            AddProperty(mappingUpdateRef, property);
                        }
                    }
            }

            RunSchemaUpdate(updateCfg);
            updateCfg = null;
            mappingUpdate = null;

            foreach (var dynClass in pClasses)
            {
                if (dynClass.Properties != null)
                    foreach (var property in dynClass.Properties)
                    {
                        if (property.IsReference)
                        {
                            AddProperty(mappingCfg, property);
                            AddProperty(mappingUpdateRef, property);
                        }
                    }
            }

            RunSchemaUpdate(updateCfgRef);
            updateCfgRef = null;
            mappingUpdateRef = null;

            MigrateIdsFromBaseToSubTables(cfg, mappingCfg, pClasses);

            return cfg;
        }

        private void RemoveFromConfiguration(Configuration pConfiguration, PersistentClass pPersistentClass)
        {
            var classes = (IDictionary<string, PersistentClass>)typeof(Configuration).GetField("classes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pConfiguration);
            classes.Remove(pPersistentClass.EntityName);

            RemoveTable(pConfiguration, pPersistentClass.Table);

            foreach (var join in pPersistentClass.JoinClosureIterator)
            {
                RemoveTable(pConfiguration, join.Table);
            }

            pConfiguration.Imports.Remove(pPersistentClass.EntityName);
            pConfiguration.Imports.Remove(pPersistentClass.ClassName);

            var propertyReferences = (IList<Mappings.PropertyReference>)typeof(Configuration).GetField("propertyReferences", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pConfiguration);

            foreach (var propRef in propertyReferences.ToArray())
            {
                if (propRef.referencedClass == pPersistentClass.ClassName)
                    propertyReferences.Remove(propRef);
            }
        }

        private void RemoveTable(Configuration pConfiguration, Table pTable)
        {
            var tables = (IDictionary<string, Table>)typeof(Configuration).GetField("tables", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pConfiguration);

            var kvtable = tables.FirstOrDefault(t => t.Value == pTable);
            if (kvtable.Key != null)
                tables.Remove(kvtable.Key);

            var collections = (IDictionary<string, Collection>)typeof(Configuration).GetField("collections", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pConfiguration);

            foreach (var key in collections.Where(t => t.Value.Table == pTable).Select(v => v.Key).ToArray())
            {
                if (key != null)
                    collections.Remove(key);
            }
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
                    if (mAlreadyMergedEntities.Contains(persistentClass.EntityName))
                        continue;

                    mAlreadyMergedEntities.Add(persistentClass.EntityName);

                    var table = mapping.IterateTables.Single(t => t.Name == TablePrefix + dynClass.Name).GetQuotedName();

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
            if (pDynClass.FullName.Contains(" "))
            {
                throw new NotSupportedException($"{nameof(pDynClass.FullName)} contains a space");
            }

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
                table = pMappings.AddTable(null, null, TablePrefix + pDynClass.Name, null, false, "all");
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

        private void AddProperty(Mappings pMapping, DynamixProperty pDynProperty)
        {
            if(pDynProperty.Name == "Id")
            {
                return;
            }

            var persistentClass = pMapping.LocatePersistentClassByEntityName(pDynProperty.DynamixClass.FullName);

            var table = pMapping.IterateTables.SingleOrDefault(t => t.Name == TablePrefix + pDynProperty.DynamixClass.Name);

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
                    var manyToOne = new ManyToOne(table)
                    {
                        PropertyName = pDynProperty.Name,
                        ReferencedEntityName = referencedPersistentClass.EntityName,
                        FetchMode = FetchMode.Join,
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

                        table.AddColumn(propColumn);
                        table.CreateForeignKey(null, new[] { propColumn }, referencedPersistentClass.EntityName);
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

                if (persistentClass is SingleTableSubclass subClass)
                {
                    if (subClass.JoinIterator.Any())
                    {
                        var join = subClass.JoinIterator.First();
                        join.AddProperty(prop);
                        return;
                    }

                    throw new InvalidOperationException("Not possible code reached");
                }

                persistentClass.AddProperty(prop);
            }
            else
            {
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

        private void RunSchemaUpdate(Configuration pConfiguration)
        {
            var dialect = Dialect.GetDialect(pConfiguration.Properties);

            var changedTables = GetChangedTables(pConfiguration, dialect);

            var tables = (IDictionary<string, Table>)typeof(Configuration).GetField("tables", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pConfiguration);

            foreach (var tableEntry in tables.ToArray())
            {
                if (!tableEntry.Value.IsPhysicalTable)
                {
                    continue;
                }

                var name = tableEntry.Value.GetQualifiedName(dialect);

                if (!changedTables.Contains(name))
                {
                    tables.Remove(tableEntry.Key);
                }
            }

            var update = new SchemaUpdate(pConfiguration);
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

        private List<string> GetChangedTables(Configuration pConfiguration, Dialect pDialect)
        {
            var ret = new List<string>();

            var sqls = pConfiguration.GenerateSchemaCreationScript(pDialect);

            var grouped = sqls.GroupBy(s => SqlHelper.GetTableName(s)).ToArray();
            var createsqls = grouped.ToDictionary(v => v.Key, v => string.Join(string.Empty, v));

            foreach (var table in createsqls)
            {
                if (!mLastCreationSqls.ContainsKey(table.Key))
                {
                    ret.Add(table.Key);
                    continue;
                }

                if (table.Value != mLastCreationSqls[table.Key])
                {
                    ret.Add(table.Key);
                }
            }

            mLastCreationSqls = createsqls;

            return ret;
        }
    }
}
