using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Core.ServiceMesh.SourceGen.Core;

internal sealed class ImmutableEquatableArray<T> : IEquatable<ImmutableEquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly T[] _values;

    public ImmutableEquatableArray(T[] values)
    {
        _values = values;
    }

    public ImmutableEquatableArray(IEnumerable<T> values)
    {
        _values = values.ToArray();
    }

    public static ImmutableEquatableArray<T> Empty { get; } = new(Array.Empty<T>());

    public bool Equals(ImmutableEquatableArray<T> other)
    {
        return other != null && ((ReadOnlySpan<T>)_values).SequenceEqual(other._values);
    }

    public T this[int index] => _values[index];
    public int Count => _values.Length;

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)_values).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _values.GetEnumerator();
    }

    public override bool Equals(object obj)
    {
        return obj is ImmutableEquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var value in _values) hash = Combine(hash, value.GetHashCode());

        static int Combine(int h1, int h2)
        {
            // RyuJIT optimizes this to use the ROL instruction
            // Related GitHub pull request: https://github.com/dotnet/coreclr/pull/1830
            var rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)rol5 + h1) ^ h2;
        }

        return hash;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(_values);
    }

    public struct Enumerator
    {
        private readonly T[] _values;
        private int _index;

        internal Enumerator(T[] values)
        {
            _values = values;
            _index = -1;
        }

        public bool MoveNext()
        {
            var newIndex = _index + 1;

            if ((uint)newIndex < (uint)_values.Length)
            {
                _index = newIndex;
                return true;
            }

            return false;
        }

        public readonly T Current => _values[_index];
    }
}