using Core.ServiceMesh.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.ServiceMesh.SourceGen.Tests;

public static class TestHelper
{
    public static async Task VerifySourceGen(string source)
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