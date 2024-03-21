using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MinHook.SourceGenerators;

readonly struct HookGeneratorContext
{
    public required GeneratorAttributeSyntaxContext AttributeSyntaxContext { get; init; }

    public required ClassDeclarationSyntax ClassDeclarationSyntax { get; init; }

    public required INamedTypeSymbol TypeSymbol { get; init; }

    public required INamedTypeSymbol TargetDelegateType { get; init; }

    public required string ModuleName { get; init; }
    public required string FunctionName { get; init; }
}
