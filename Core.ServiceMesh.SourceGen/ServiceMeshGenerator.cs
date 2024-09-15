using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Core.ServiceMesh.SourceGen.Core;
using Core.ServiceMesh.SourceGen.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.ServiceMesh.SourceGen;

[Generator]
public sealed class ServiceMeshGenerator : IIncrementalGenerator
{
    private const string AttributeName = "ServiceMeshAttribute";
    private const string QualifiedAttributeName = $"Core.ServiceMesh.Abstractions.{AttributeName}";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaces = context
            .SyntaxProvider.ForAttributeWithMetadataName(QualifiedAttributeName, Predicate, Transform)
            .Collect();

        context.RegisterSourceOutput(interfaces, GenerateCode);
    }

    private bool Predicate(SyntaxNode node, CancellationToken _)
    {
        return node is InterfaceDeclarationSyntax || node is ClassDeclarationSyntax;
    }

    private ServiceDescription Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken _)
    {
        var typeSymbol = (ITypeSymbol)ctx.TargetSymbol;
        var isInterface = typeSymbol.TypeKind == TypeKind.Interface;

        var className = typeSymbol.Name;
        var attr = typeSymbol.GetAttributes().Single(x => x.AttributeClass!.Name == AttributeName);
        var serviceName = (string)attr.ConstructorArguments[0].Value;

        var methods = typeSymbol.GetMembers()
            .Where(x => x.Kind == SymbolKind.Method)
            .OfType<IMethodSymbol>()
            .Where(x => x.DeclaredAccessibility == Accessibility.Public)
            .Where(x => x.MethodKind == MethodKind.Ordinary)
            .Where(x => !x.IsStatic)
            .ToList();

        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : $"{typeSymbol.ContainingNamespace}";

        return new ServiceDescription(isInterface, className, ns, serviceName, methods.Select(x =>
        {
            var methodName = x.Name;
            var generics = x
                .TypeParameters.Select(arg => arg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                .ToList();
            var constraints = x
                .TypeParameters.Select(arg => arg.GetWhereStatement())
                .ToList();
            var parameters = x.Parameters.Select(y => y.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                .ToList();
            var paramNames = x.Parameters.Select(y => y.Name).ToList();
            var returnType = x.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            List<string> returnTypeArgs = [];

            if (x.ReturnType is INamedTypeSymbol namedReturnType)
                foreach (var arg in namedReturnType.TypeArguments)
                    returnTypeArgs.Add(arg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            return new MethodDescription(methodName, returnType, returnTypeArgs, parameters, paramNames, generics,
                constraints);
        }).ToList(), GetUsings(typeSymbol).ToList());
    }

    private static void GenerateCode(SourceProductionContext context, ImmutableArray<ServiceDescription> services)
    {
        foreach (var service in services)
            if (service.IsInterface)
                BuildRemoteProxy(context, service);
            else
                BuildTraceProxy(context, service);
    }

    private static void BuildRemoteProxy(SourceProductionContext context, ServiceDescription service)
    {
        var builder = new StringBuilder();

        foreach (var use in service.Usings)
            builder.AppendLine(use);

        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(service.Namespace))
        {
            builder.AppendLine($"namespace {service.Namespace};");
            builder.AppendLine();
        }

        builder.AppendLine("[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine(
            $"public sealed class {service.ClassName}RemoteProxy(IServiceMesh mesh) : {service.ClassName}");
        builder.AppendLine("{");

        foreach (var m in service.Methods)
        {
            builder.AppendLine();
            var parameterEx = $"[{string.Join(", ", m.ParameterNames)}]";
            var genericsEx = $"[{string.Join(", ", m.Generics.Select(x => $"typeof({x})"))}]";
            var invoke = $"await mesh.RequestAsync(subject, {parameterEx}, {genericsEx});";

            if (m.Return.StartsWith("ValueTask<"))
                invoke =
                    $"return await mesh.RequestAsync<{m.ReturnArguments[0]}>(subject, {parameterEx}, {genericsEx});";

            if (m.Return.StartsWith("IAsyncEnumerable<"))
                invoke = $"""
                          await foreach (var msg in mesh.StreamAsync<{m.ReturnArguments[0]}>(subject, {parameterEx}, {genericsEx}))
                                      yield return msg;
                          """;

            var code = $$"""
                             public async {{m.Return}} {{m.Name}}{{(m.Generics.Any() ? "<" + string.Join(",", m.Generics.Select(x => x)) + ">" : string.Empty)}}({{string.Join(", ", m.Parameters.Select(x => x))}}) {{(m.Constraints.Any() ? string.Join(", ", m.Constraints) : string.Empty)}}
                             {
                                 var subject = "{{service.ServiceName}}.{{m.Name}}.G{{m.Generics.Count}}P{{m.Parameters.Count}}";
                                 {{invoke}}
                             }
                         """;
            builder.AppendLine(code);
        }

        builder.AppendLine("}");

        context.AddSource($"{service.Namespace}.{service.ClassName}RemoteProxy.g.cs", builder.ToString());
    }

    private static void BuildTraceProxy(SourceProductionContext context, ServiceDescription service)
    {
        var builder = new StringBuilder();

        foreach (var use in service.Usings)
            builder.AppendLine(use);

        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(service.Namespace))
        {
            builder.AppendLine($"namespace {service.Namespace};");
            builder.AppendLine();
        }

        builder.AppendLine("[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine(
            $"public sealed class {service.ClassName}TraceProxy({service.ClassName} svc) : I{service.ClassName}");
        builder.AppendLine("{");

        foreach (var m in service.Methods)
        {
            builder.AppendLine();

            // TODO: use await for correct trace?
            var code = $$"""
                             public {{m.Return}} {{m.Name}}{{(m.Generics.Any() ? "<" + string.Join(",", m.Generics.Select(x => x)) + ">" : string.Empty)}}({{string.Join(", ", m.Parameters.Select(x => x))}}) {{(m.Constraints.Any() ? string.Join(", ", m.Constraints) : string.Empty)}}
                             {
                                using var activity = ServiceMeshActivity.Source.StartActivity("REQ {{service.ServiceName}}.{{m.Name}}", ActivityKind.Internal, Activity.Current?.Context ?? default);
                                return svc.{{m.Name}}({{string.Join(", ", m.ParameterNames)}}); 
                             }
                         """;

            builder.AppendLine(code);
        }

        builder.AppendLine("}");

        context.AddSource($"{service.Namespace}.{service.ClassName}TraceProxy.g.cs", builder.ToString());
    }

    private static IEnumerable<string> GetUsings(ISymbol classSymbol)
    {
        var allUsings = SyntaxFactory.List<UsingDirectiveSyntax>();
        foreach (var syntaxRef in classSymbol.DeclaringSyntaxReferences)
            allUsings = syntaxRef
                .GetSyntax()
                .Ancestors(false)
                .Aggregate(
                    allUsings,
                    (current, parent) =>
                        parent switch
                        {
                            NamespaceDeclarationSyntax ndSyntax
                                => current.AddRange(ndSyntax.Usings),
                            CompilationUnitSyntax cuSyntax => current.AddRange(cuSyntax.Usings),
                            _ => current
                        }
                );

        return [.. allUsings.Select(x => x.ToString())];
    }
}