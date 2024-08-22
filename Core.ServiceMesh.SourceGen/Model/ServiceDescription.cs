using System.Collections.Generic;
using Core.ServiceMesh.SourceGen.Core;

namespace Core.ServiceMesh.SourceGen.Model;

internal readonly record struct ServiceDescription
{
    public readonly string Name;
    public readonly ImmutableEquatableArray<MethodDescription> Methods;

    public ServiceDescription(string name, List<MethodDescription> methods)
    {
        Name = name;
        Methods = new(methods);
    }
}