using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;

namespace DynamixGenerator
{
    public class DynamixCompiler
    {
        private ReferenceHelper mReferenceHelper;

        public DynamixCompiler(ReferenceHelper pReferenceHelper)
        {
            mReferenceHelper = pReferenceHelper;
        }

        public byte[] CompileCode(string pAssemblyName, string pCode)
        {
            var parseOptions = new CSharpParseOptions(kind: SourceCodeKind.Regular, languageVersion: LanguageVersion.Latest);
            var syntaxTree = CSharpSyntaxTree.ParseText(pCode, parseOptions);

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithPlatform(Platform.AnyCpu);
            var references = mReferenceHelper.GetMetadataReferences(a => a.FullName.StartsWith(pAssemblyName));

            var compilation = CSharpCompilation.Create(pAssemblyName)
              .WithOptions(compilationOptions)
              .AddReferences(references)
              .AddSyntaxTrees(syntaxTree);

            using MemoryStream msDll = new();

            var result = compilation.Emit(peStream: msDll);

            if (!result.Success)
            {
                throw new Exception("Generated code did not compile!");
            }

            msDll.Seek(0, SeekOrigin.Begin);

            return msDll.ToArray();
        }
    }
}
