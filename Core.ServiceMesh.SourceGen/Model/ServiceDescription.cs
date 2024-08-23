using System.Collections.Generic;
using Core.ServiceMesh.SourceGen.Core;

namespace Core.ServiceMesh.SourceGen.Model;

internal readonly record struct ServiceDescription
{
    public readonly string ClassName;
    public readonly string Namespace;
    public readonly bool IsInterface;
    public readonly string ServiceName;
    public readonly ImmutableEquatableArray<MethodDescription> Methods;
    public readonly ImmutableEquatableArray<string> Usings;

    public ServiceDescription(bool isInterface, string className, string ns, string service, List<MethodDescription> methods, List<string> usings)
    {
        IsInterface = isInterface;
        ClassName = className;
        Namespace = ns;
        ServiceName = service;
        Methods = new(methods);
        Usings = new(usings);
    }
}