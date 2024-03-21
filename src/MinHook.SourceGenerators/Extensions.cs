using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Silkworm.SourceGenerators;

internal static class Extensions
{
    public static INamedTypeSymbol? GetTypeArgumentSymbol(this AttributeData attributeData, int index)
    {
        if (attributeData.AttributeClass is null)
        {
            return null;
        }

        var typeArguments = attributeData.AttributeClass.TypeArguments;
        return typeArguments[index] as INamedTypeSymbol;
    }

    public static T? GetAttributeArgument<T>(this AttributeData attributeData, int index) where T : class
    {
        if (attributeData.ConstructorArguments.Length <= index)
        {
            return null;
        }

        var arg = attributeData.ConstructorArguments[index];
        return arg.Value as T;
    }

    public static T If<T>(this T syntaxNode, bool condition, Func<T, T> func) where T : SyntaxNode
    {
        return condition ? func(syntaxNode) : syntaxNode;
    }

    public static T If<T>(this T syntaxNode, Func<T, bool> condition, Func<T, T> func) where T : SyntaxNode
    {
        return condition(syntaxNode) ? func(syntaxNode) : syntaxNode;
    }

    public static ParameterSyntax ToSyntax(this IParameterSymbol parameterSymbol)
    {
        string parameterName = parameterSymbol.Name;
        ITypeSymbol parameterType = parameterSymbol.Type;

        TypeSyntax typeSyntax = SyntaxFactory.ParseTypeName(parameterType.ToDisplayString());

        ParameterSyntax parameterSyntax = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(typeSyntax);

        if (parameterSymbol.RefKind != RefKind.None)
        {
            switch (parameterSymbol.RefKind)
            {
                case RefKind.Ref:
                    parameterSyntax = parameterSyntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.RefKeyword));
                    break;
                case RefKind.Out:
                    parameterSyntax = parameterSyntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.OutKeyword));
                    break;
                case RefKind.In:
                    parameterSyntax = parameterSyntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.InKeyword));
                    break;
                default:
                    break;
            }
        }

        return parameterSyntax;
    }

    public static IncrementalValuesProvider<TResult> OfType<TSource, TResult>(
        this IncrementalValuesProvider<TSource> source) where TResult : TSource
    {
        return source.Where(item => item is TResult).Select((item, token) => (TResult)item!);
    }
}
