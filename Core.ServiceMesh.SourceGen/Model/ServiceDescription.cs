using System.Collections.Generic;
using Core.ServiceMesh.SourceGen.Core;

namespace Core.ServiceMesh.SourceGen.Model;

internal readonly record struct ServiceDescription
{
    public readonly string ClassName;
    public readonly string InterFaceName;
    public readonly bool IsInterface;
    public readonly ImmutableEquatableArray<MethodDescription> Methods;
    public readonly string Namespace;
    public readonly string ServiceName;
    public readonly ImmutableEquatableArray<string> Usings;

    public ServiceDescription(bool isInterface, string className, string interfaceName, string ns, string service,
        List<MethodDescription> methods, List<string> usings)
    {
        IsInterface = isInterface;
        ClassName = className;
        InterFaceName = interfaceName;
        Namespace = ns;
        ServiceName = service;
        Methods = new ImmutableEquatableArray<MethodDescription>(methods);
        Usings = new ImmutableEquatableArray<string>(usings);
    }
}