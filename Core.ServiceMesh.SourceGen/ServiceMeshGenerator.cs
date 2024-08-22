using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.ServiceMesh.SourceGen
{
    [Generator]
    public sealed class ServiceMeshGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var interfaces = context
                .SyntaxProvider.ForAttributeWithMetadataName(
                    "Core.ServiceMesh.Abstractions.ServiceMeshAttribute",
                    (node, _) => node is InterfaceDeclarationSyntax,
                    (ctx, _) => (ITypeSymbol)ctx.TargetSymbol
                )
                .Collect();

            context.RegisterSourceOutput(interfaces, GenerateCode);
        }

        private static void GenerateCode(
            SourceProductionContext context,
            ImmutableArray<ITypeSymbol> enumerations
        )
        {
            if (enumerations.IsDefaultOrEmpty) return;

            foreach (var type in enumerations)
            {
                var serviceName = type.Name;

                var methods = type.GetMembers()
                    .Where(x => x.Kind == SymbolKind.Method)
                    .OfType<IMethodSymbol>()
                    .Where(x => x.DeclaredAccessibility == Accessibility.Public)
                    .Where(x => x.MethodKind == MethodKind.Ordinary)
                    .Where(x => !x.IsStatic).ToList();

                var classImpl = new StringBuilder();

                classImpl.AppendLine($"public sealed class {serviceName}RemoteProxy(IServiceMesh mesh)");
                classImpl.AppendLine("{");

                foreach (var method in methods)
                {
                    var methodName = method.Name;

                    var generics = method
                        .TypeParameters.Select(arg => arg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                        .ToList();

                    var parameters = method.Parameters.Select(x => x.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)).ToList();
                    var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

                    var code = $@"
    public {returnType} {methodName}{(generics.Any() ? "<" + string.Join(",", generics.Select(x => x)) + ">" : string.Empty)}({string.Join(", ", parameters.Select(x=>x))}) {{
        return mesh.DoStuff();
    }}
";
                    classImpl.AppendLine(code);
                    classImpl.AppendLine();
                }

                classImpl.AppendLine("}");

                var source = classImpl.ToString();

                context.AddSource($"{Guid.NewGuid().ToString("N")}.g.cs", source);
            }
        }
    }
}