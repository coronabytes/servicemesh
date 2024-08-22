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

                     [ServiceMesh("someservice")]
                     public interface ISomeService
                     {
                         ValueTask A(string a);
                         ValueTask<string> B(string b);
                         ValueTask<T> C<T>(T a, T b) where T : INumber<T>;
                         IAsyncEnumerable<string> D(string d);
                     }
                     """;
        return TestHelper.Verify(source);
    }
}

public static class TestHelper
{
    public static async Task Verify(string source)
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
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var sourceDiagnostics = compilation.GetDiagnostics();
        var errors = sourceDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.Empty(errors);

        var generator = new ServiceMeshGenerator();

        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var _);

        var results = driver.GetRunResult().Results;
        results.ToString();
    }
}