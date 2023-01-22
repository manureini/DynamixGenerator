using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL.Design.Internal;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using System.Runtime.Loader;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DynamixGenerator.EfCore
{
    public class DynamixSchemaUpdater
    {
        public IModel UpdateSchema(DbContext pDbContext, DynamixClass[] pDynamixClasses, Action<string> pLoggerCallback)
        {
            var dynamixContext = (IDynamixDbContext)pDbContext;

            dynamixContext.OnModelBuildAction = (modelBuilder) =>
            {
                foreach (var dynamix in pDynamixClasses)
                {
                    var baseType = dynamix.GetTypeReference().BaseType;

                    var bs = modelBuilder.Entity(baseType);

                    var hasDiscriminator = bs.Metadata.FindAnnotation("DiscriminatorValue") != null;

                    var entity = modelBuilder.Entity(dynamix.GetTypeReference());

                    if (!hasDiscriminator)
                    {
                        entity = entity.ToTable("_" + dynamix.Name);
                    }

                    /*
                    foreach (var property in dynamix.Properties)
                    {
                        if (property.Name == "BusinessLicence")
                            continue;

                        if (property.Name == "Kiab")
                            continue;

                        entity.Property(property.Name);
                    }*/


                    if (baseType.ToString().Contains("FormValues"))
                    {
                        entity = entity.ToTable("_" + dynamix.Name);
                    }
                }
            };

            IServiceCollection services = new ServiceCollection();
            services.AddDbContextDesignTimeServices(pDbContext);
            services.AddEntityFrameworkDesignTimeServices();

#pragma warning disable EF1001 // Internal EF Core API usage.
            new NpgsqlDesignTimeServices().ConfigureDesignTimeServices(services);
#pragma warning restore EF1001 // Internal EF Core API usage.

            var serviceProvider = services.BuildServiceProvider();

            var scaffolder = serviceProvider.GetRequiredService<IReverseEngineerScaffolder>();

            var dbOpts = new DatabaseModelFactoryOptions();
            // Use the database schema names directly
            var modelOpts = new ModelReverseEngineerOptions();
            var codeGenOpts = new ModelCodeGenerationOptions()
            {
                // Set namespaces
                RootNamespace = "TypedDataContext",
                ContextName = "DataContext",
                ContextNamespace = "TypedDataContext.Context",
                ModelNamespace = "TypedDataContext.Models",
                // We are not afraid of the connection string in the source code, 
                // because it will exist only in runtime
                SuppressConnectionStringWarning = true
            };

            ScaffoldedModel scaffoldedModelSources = scaffolder.ScaffoldModel(pDbContext.Database.GetConnectionString(), dbOpts, modelOpts, codeGenOpts);

            var sourceFiles = new List<string> { scaffoldedModelSources.ContextFile.Code };
            sourceFiles.AddRange(scaffoldedModelSources.AdditionalFiles.Select(f => f.Code));

            using var peStream = new MemoryStream();

            var enableLazyLoading = false;
            var result = GenerateCode(sourceFiles, enableLazyLoading).Emit(peStream);

            if (!result.Success)
            {
                var failures = result.Diagnostics
                    .Where(diagnostic => diagnostic.IsWarningAsError ||
                                         diagnostic.Severity == DiagnosticSeverity.Error);

                var error = failures.FirstOrDefault();
                throw new Exception($"{error?.Id}: {error?.GetMessage()}");
            }

            var infrastructure = pDbContext.GetInfrastructure();
            var migSqlGen = infrastructure.GetService<IMigrationsSqlGenerator>();
            var modelDiffer = infrastructure.GetService<IMigrationsModelDiffer>();
            var conn = infrastructure.GetService<IRelationalConnection>();
            var designTimeModel = infrastructure.GetService<IDesignTimeModel>();

            var model = designTimeModel.Model;

            var assemblyLoadContext = new AssemblyLoadContext("DbContext_generated", isCollectible: !enableLazyLoading);

            peStream.Seek(0, SeekOrigin.Begin);
            var assembly = assemblyLoadContext.LoadFromStream(peStream);

            var type = assembly.GetType($"{codeGenOpts.ContextNamespace}.{codeGenOpts.ContextName}");
            _ = type ?? throw new Exception("DataContext type not found");

            var constr = type.GetConstructor(Type.EmptyTypes);
            _ = constr ?? throw new Exception("DataContext ctor not found");

            DbContext dynamicContext = (DbContext)constr.Invoke(null);

            var dynamicModel = dynamicContext.GetService<IDesignTimeModel>().Model.GetRelationalModel();

            var diffs = modelDiffer.GetDifferences(dynamicModel, model.GetRelationalModel());
            var sqlcmds = migSqlGen.Generate(diffs, model, MigrationsSqlGenerationOptions.Default);

            string totalsql = string.Join(Environment.NewLine, sqlcmds.Select(s => s.CommandText));

            foreach (var cmd in sqlcmds)
            {
                if (cmd.CommandText.Contains("DROP TABLE"))
                    continue;

                pLoggerCallback?.Invoke(cmd.CommandText);

                try
                {
                    cmd.ExecuteNonQuery(conn);
                }
                catch (Exception ex) when (ex.Message.Contains("There is already an object named") || ex.Message.Contains("existiert bereits") || ex.Message.Contains("existiert nicht"))
                {
                    // Ignore
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            if (!enableLazyLoading)
            {
                assemblyLoadContext.Unload();
            }

            return model;
        }

        private static List<MetadataReference> CompilationReferences(bool enableLazyLoading)
        {
            var refs = new List<MetadataReference>();
            var referencedAssemblies = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
            refs.AddRange(referencedAssemblies.Select(a => MetadataReference.CreateFromFile(Assembly.Load(a).Location)));

            refs.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(Assembly.Load("netstandard, Version=2.0.0.0").Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(System.Data.Common.DbConnection).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Expressions.Expression).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(Microsoft.EntityFrameworkCore.DeleteBehavior).Assembly.Location));

            if (enableLazyLoading)
            {
                //    refs.Add(MetadataReference.CreateFromFile(typeof(ProxiesExtensions).Assembly.Location));
            }

            return refs;
        }

        private static CSharpCompilation GenerateCode(List<string> sourceFiles, bool enableLazyLoading)
        {
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

            //  sourceFiles.Insert(0, "using Microsoft.EntityFrameworkCore;");

            var parsedSyntaxTrees = sourceFiles.Select(f => SyntaxFactory.ParseSyntaxTree(f, options));

            return CSharpCompilation.Create($"DataContext.dll",
                parsedSyntaxTrees,
                references: CompilationReferences(enableLazyLoading),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));
        }
    }
}