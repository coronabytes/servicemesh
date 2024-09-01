using System.Collections.Generic;
using Core.ServiceMesh.SourceGen.Core;

namespace Core.ServiceMesh.SourceGen.Model;

internal readonly record struct MethodDescription
{
    public readonly string Name;
    public readonly string Return;
    public readonly ImmutableEquatableArray<string> ReturnArguments;
    public readonly ImmutableEquatableArray<string> Parameters;
    public readonly ImmutableEquatableArray<string> ParameterNames;
    public readonly ImmutableEquatableArray<string> Generics;
    public readonly ImmutableEquatableArray<string> Constraints;

    public MethodDescription(string name, string ret, List<string> returnArgs, List<string> parameter, List<string> parameterNames, List<string> generics, List<string> constraints)
    {
        Name = name;
        Return = ret;
        ReturnArguments = new(returnArgs);
        Parameters = new(parameter);
        ParameterNames = new(parameterNames);
        Generics = new(generics);
        Constraints = new(constraints);
    }
}