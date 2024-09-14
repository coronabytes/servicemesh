using System.Collections.Generic;
using Core.ServiceMesh.SourceGen.Core;

namespace Core.ServiceMesh.SourceGen.Model;

internal readonly record struct MethodDescription
{
    public readonly ImmutableEquatableArray<string> Constraints;
    public readonly ImmutableEquatableArray<string> Generics;
    public readonly string Name;
    public readonly ImmutableEquatableArray<string> ParameterNames;
    public readonly ImmutableEquatableArray<string> Parameters;
    public readonly string Return;
    public readonly ImmutableEquatableArray<string> ReturnArguments;

    public MethodDescription(string name, string ret, List<string> returnArgs, List<string> parameter,
        List<string> parameterNames, List<string> generics, List<string> constraints)
    {
        Name = name;
        Return = ret;
        ReturnArguments = new ImmutableEquatableArray<string>(returnArgs);
        Parameters = new ImmutableEquatableArray<string>(parameter);
        ParameterNames = new ImmutableEquatableArray<string>(parameterNames);
        Generics = new ImmutableEquatableArray<string>(generics);
        Constraints = new ImmutableEquatableArray<string>(constraints);
    }
}