using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;

namespace DynamixGenerator
{
    public class DynamixCompiler
    {
        public string AssemblyName { get; protected set; }

        private ReferenceHelper mReferenceHelper;

        public DynamixCompiler(string pAssemblyName)
        {
            AssemblyName = pAssemblyName;
            mReferenceHelper = new ReferenceHelper(a => a.FullName.StartsWith(AssemblyName));
        }

        public byte[] CompileCode(string pCode) 
        {
            var parseOptions = new CSharpParseOptions(kind: SourceCodeKind.Regular, languageVersion: LanguageVersion.Latest);
            var syntaxTree = CSharpSyntaxTree.ParseText(pCode, parseOptions);

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithPlatform(Platform.AnyCpu);
            var references = mReferenceHelper.GetMetadataReferences();

            var compilation = CSharpCompilation.Create(AssemblyName)
              .WithOptions(compilationOptions)
              .AddReferences(references)
              .AddSyntaxTrees(syntaxTree);

            using MemoryStream msDll = new();

            var result = compilation.Emit(peStream: msDll);

            if(!result.Success)
            {
                throw new Exception("Generated code did not compile!");
            }

            msDll.Seek(0, SeekOrigin.Begin);

            return msDll.ToArray();
        }
    }
}
