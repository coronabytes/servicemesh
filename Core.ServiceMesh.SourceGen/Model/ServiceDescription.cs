using System.Collections.Generic;
using Core.ServiceMesh.SourceGen.Core;

namespace Core.ServiceMesh.SourceGen.Model;

internal readonly record struct ServiceDescription
{
    public readonly string ClassName;
    public readonly string ServiceName;
    public readonly ImmutableEquatableArray<MethodDescription> Methods;

    public ServiceDescription(string className, string service, List<MethodDescription> methods)
    {
        ClassName = className;
        ServiceName = service;
        Methods = new(methods);
    }
}