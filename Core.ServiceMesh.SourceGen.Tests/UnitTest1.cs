using Core.ServiceMesh.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.ServiceMesh.SourceGen.Tests;

public class UnitTest1
{
    [Fact]
    public Task ProxyInterface()
    {
        var source = """
                     using System.Collections.Generic;
                     using System.Threading.Tasks;
                     using System.Numerics;
                     using Core.ServiceMesh.Abstractions;
                     
                     namespace SampleApp;

                     [ServiceMesh("someservice")]
                     public interface ISomeService
                     {
                         ValueTask A(string a);
                         ValueTask<string> B(string b);
                         ValueTask<T> C<T>(T a, T b) where T : INumber<T>;
                         IAsyncEnumerable<string> D(string d);
                     }
                     
                     [ServiceMesh("someservice")]
                     public class SomeService : ISomeService
                     {
                         public async ValueTask A(string a)
                         {
                            
                         }
                         public async ValueTask<string> B(string b)
                         {
                            return b + " " + b;
                         }
                         public async ValueTask<T> C<T>(T a, T b) where T : INumber<T>
                         {
                            return a + b;
                         }
                         public async IAsyncEnumerable<string> D(string d)
                         {
                            yield return "a";
                            yield return "b";
                            yield return "c";
                         }
                     }
                     """;
        return TestHelper.VerifySourceGen(source);
    }
}

public static class TestHelper
{
    public async static Task VerifySourceGen(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // force reference
        typeof(ServiceMeshAttribute).ToString();

        var references = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>();

        var compilation = CSharpCompilation.Create(
            "SourceGeneratorTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var sourceDiagnostics = compilation.GetDiagnostics();
        var errors = sourceDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.Empty(errors);

        var generator = new ServiceMeshGenerator();

        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        foreach (var tree in outputCompilation.SyntaxTrees.Skip(1))
        {
            var genSource = tree.ToString();
            var genErrors = tree.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error);

            Assert.Empty(genErrors);
        }
    }
}