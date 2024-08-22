using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Core.ServiceMesh.SourceGen.Core;
using Core.ServiceMesh.SourceGen.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.ServiceMesh.SourceGen
{
    [Generator]
    public sealed class ServiceMeshGenerator : IIncrementalGenerator
    {
        private const string QualifiedAttributeName = "Core.ServiceMesh.Abstractions.ServiceMeshAttribute";
        private const string AttributeName = "ServiceMeshAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var interfaces = context
                .SyntaxProvider.ForAttributeWithMetadataName(QualifiedAttributeName, Predicate, Transform)
                .Collect();

            context.RegisterSourceOutput(interfaces, GenerateCode);
        }

        private bool Predicate(SyntaxNode node, CancellationToken _)
        {
            return node is InterfaceDeclarationSyntax;
        }

        private ServiceDescription Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken _)
        {
            var type = (ITypeSymbol)ctx.TargetSymbol;
            var serviceName = type.Name;

            var attr = type.GetAttributes().Single(x => x.AttributeClass!.Name == AttributeName);
            var serviceName2 = (string)attr.ConstructorArguments[0].Value;

            var methods = type.GetMembers()
                .Where(x => x.Kind == SymbolKind.Method)
                .OfType<IMethodSymbol>()
                .Where(x => x.DeclaredAccessibility == Accessibility.Public)
                .Where(x => x.MethodKind == MethodKind.Ordinary)
                .Where(x => !x.IsStatic)
                .ToList();

            return new ServiceDescription(serviceName, methods.Select(x=>
            {
                var methodName = x.Name;
                var generics = x
                    .TypeParameters.Select(arg => arg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                    .ToList();
                var constraints = x
                    .TypeParameters.Select(arg => arg.GetWhereStatement())
                    .ToList();
                var parameters = x.Parameters.Select(x => x.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)).ToList();
                var returnType = x.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

                return new MethodDescription(methodName, returnType, parameters, generics, constraints);
            }).ToList());
        }

        private static void GenerateCode(
            SourceProductionContext context,
            ImmutableArray<ServiceDescription> services
        )
        {
            if (services.IsDefaultOrEmpty) 
                return;

            foreach (var service in services)
            {
                var builder = new StringBuilder();

                builder.AppendLine($"public sealed class {service.Name}RemoteProxy(IServiceMesh mesh)");
                builder.AppendLine("{");

                foreach (var m in service.Methods)
                {
                    var code = $@"
    public {m.Return} {m.Name}{(m.Generics.Any() ? "<" + string.Join(",", m.Generics.Select(x => x)) + ">" : string.Empty)}({string.Join(", ", m.Parameters.Select(x=>x))}) {(m.Constraints.Any() ? string.Join(", ", m.Constraints) : string.Empty)} {{
        var subject = ""{service.Name}.{m.Name}.G{m.Generics.Count}P{m.Parameters.Count}"";
        return mesh.DoStuff();
    }}";
                    builder.AppendLine(code);
                }

                builder.AppendLine("}");

                var source = builder.ToString();
                source.ToString();

                //context.AddSource($"{Guid.NewGuid().ToString("N")}.g.cs", source);
            }
        }
    }
}