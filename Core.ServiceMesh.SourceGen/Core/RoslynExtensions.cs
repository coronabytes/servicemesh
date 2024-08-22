using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Core.ServiceMesh.SourceGen.Core;

internal static class RoslynExtensions
{
    public static string GetWhereStatement(this ITypeParameterSymbol typeParameterSymbol)
    {
        var result = $"where {typeParameterSymbol.Name} : ";

        var constraints = "";

        var isFirstConstraint = true;

        if (typeParameterSymbol.HasReferenceTypeConstraint)
        {
            constraints += "class";
            isFirstConstraint = false;
        }

        if (typeParameterSymbol.HasValueTypeConstraint)
        {
            constraints += "struct";
            isFirstConstraint = false;
        }

        foreach (var constraintType in typeParameterSymbol.ConstraintTypes)
        {
            // if not first constraint prepend with comma
            if (!isFirstConstraint)
                constraints += ", ";
            else
                isFirstConstraint = false;

            constraints += constraintType.GetFullTypeString();
        }

        if (string.IsNullOrEmpty(constraints)) return "";

        result += constraints;

        return result;
    }

    private static string GetFullTypeString(this ISymbol type)
    {
        return type.Name
               + type.GetTypeArgsStr(symbol => ((INamedTypeSymbol)symbol).TypeArguments);
    }

    private static string GetTypeArgsStr(
        this ISymbol symbol,
        Func<ISymbol, ImmutableArray<ITypeSymbol>> typeArgGetter
    )
    {
        var typeArgs = typeArgGetter(symbol);

        if (!typeArgs.Any())
            return string.Empty;

        var stringsToAdd = new List<string>();
        foreach (var arg in typeArgs)
        {
            string strToAdd;

            if (arg is ITypeParameterSymbol typeParameterSymbol)
            {
                // this is a generic argument
                strToAdd = typeParameterSymbol.Name;
            }
            else
            {
                // this is a generic argument value.
                var namedTypeSymbol = arg as INamedTypeSymbol;
                strToAdd = namedTypeSymbol!.GetFullTypeString();
            }

            stringsToAdd.Add(strToAdd);
        }

        var result = $"<{string.Join(", ", stringsToAdd)}>";

        return result;
    }
}