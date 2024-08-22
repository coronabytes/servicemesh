using System.Collections.Generic;
using Core.ServiceMesh.SourceGen.Core;

namespace Core.ServiceMesh.SourceGen.Model;

internal readonly record struct MethodDescription
{
    public readonly string Name;
    public readonly string Return;
    public readonly ImmutableEquatableArray<string> Parameters;
    public readonly ImmutableEquatableArray<string> Generics;

    public MethodDescription(string name, string ret, List<string> parameter, List<string> generics)
    {
        Name = name;
        Return = ret;
        Parameters = new(parameter);
        Generics = new(generics);
    }
}