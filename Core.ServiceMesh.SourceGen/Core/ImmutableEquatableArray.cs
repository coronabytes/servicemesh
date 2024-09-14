using System;
using System.Collections.Generic;

namespace Core.ServiceMesh.SourceGen.Core;

internal static class ImmutableEquatableArray
{
    public static ImmutableEquatableArray<T> Empty<T>()
        where T : IEquatable<T>
    {
        return ImmutableEquatableArray<T>.Empty;
    }

    public static ImmutableEquatableArray<T> ToImmutableEquatableArray<T>(this IEnumerable<T> values)
        where T : IEquatable<T>
    {
        return values == null ? Empty<T>() : new ImmutableEquatableArray<T>(values);
    }
}