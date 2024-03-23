using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Silkworm.SourceGenerators;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using DelegateDeclarationSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax;

namespace MinHook.SourceGenerators;

[Generator]
internal class HooksGenerator : IIncrementalGenerator
{
    const string StaticLazyHookAttributeName = "MinHook.Attributes.StaticLazyHookAttribute`1";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var staticHookInput = context.SyntaxProvider.ForAttributeWithMetadataName<HookGeneratorContext?>(StaticLazyHookAttributeName,
            (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
            (attributeContext, _) =>
            {
                if (attributeContext.Attributes is not [var attributeData])
                {
                    return null;
                }

                return new HookGeneratorContext
                {
                    AttributeSyntaxContext = attributeContext,
                    ClassDeclarationSyntax = (ClassDeclarationSyntax)attributeContext.TargetNode,
                    TypeSymbol = (INamedTypeSymbol)attributeContext.TargetSymbol,
                    TargetDelegateType = attributeData.GetTypeArgumentSymbol(0)!,
                    ModuleName = attributeData.GetAttributeArgument<string>(0)!,
                    FunctionName = attributeData.GetAttributeArgument<string>(1)!
                };
            })
            .Where(item => item.HasValue)
            .Select((item, _) => item!.Value)
            .Collect();

        context.RegisterSourceOutput(staticHookInput, ((spc, generatorContexts) =>
        {
            foreach (var generationContext in generatorContexts)
            {
                spc.AddSource($"{generationContext.ClassDeclarationSyntax.Identifier.Text}.StaticLazyHook.g.cs",
                    StaticLazyHookCompilationUnit(generationContext).GetText(Encoding.UTF8));
            }
        }));
    }

    private static CompilationUnitSyntax StaticLazyHookCompilationUnit(HookGeneratorContext generatorContext)
    {
        return CompilationUnit()
            .If(!generatorContext.AttributeSyntaxContext.TargetSymbol.ContainingNamespace.IsGlobalNamespace,
                (compilationUnit => compilationUnit.AddMembers(FileScopedNamespaceDeclaration(IdentifierName(generatorContext.AttributeSyntaxContext.TargetSymbol
                    .ContainingNamespace.ToString())))))
            .AddMembers(StaticLazyHookClassDeclaration(generatorContext))
            .NormalizeWhitespace();

        static ClassDeclarationSyntax StaticLazyHookClassDeclaration(HookGeneratorContext generatorContext)
        {
            return ClassDeclaration(generatorContext.ClassDeclarationSyntax.Identifier)
                .AddModifiers(Token(SyntaxKind.PartialKeyword))
                .AddMembers(HookFieldDeclaration(generatorContext))
                .AddMembers(EnabledPropertyDeclaration())
                .AddMembers(DetourMethodDeclaration(generatorContext))
                .AddMembers(OriginalPropertyDeclaration(generatorContext))
                .AddMembers(EnableMethodDeclaration(generatorContext))
                .AddMembers(LazyEnableMethodDeclaration(generatorContext))
                .AddMembers(DisableMethodDeclaration());

            static FieldDeclarationSyntax HookFieldDeclaration(HookGeneratorContext generatorContext)
            {
                return FieldDeclaration(VariableDeclaration(NullableType(GenericName("global::MinHook.Hook")
                            .AddTypeArgumentListArguments(IdentifierName(generatorContext.TargetDelegateType.Name))))
                        .AddVariables(VariableDeclarator("_hook")))
                    .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword));
            }

            static PropertyDeclarationSyntax EnabledPropertyDeclaration()
            {
                return PropertyDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), "Enabled")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .WithExpressionBody(ArrowExpressionClause(BinaryExpression(SyntaxKind.CoalesceExpression,
                        ConditionalAccessExpression(IdentifierName("_hook"),
                            MemberBindingExpression(IdentifierName("Enabled"))),
                        LiteralExpression(SyntaxKind.DefaultLiteralExpression))))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }

            static MethodDeclarationSyntax DetourMethodDeclaration(HookGeneratorContext generationContext)
            {
                var delegateInvokeMethod = generationContext.TargetDelegateType.DelegateInvokeMethod;
                Debug.Assert(delegateInvokeMethod is not null);
                return MethodDeclaration(IdentifierName(delegateInvokeMethod!.ReturnType.ToString()), "Detour")
                    .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.PartialKeyword))
                    .AddParameterListParameters(delegateInvokeMethod.Parameters.Select(parameter => parameter.ToSyntax()).ToArray())
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }

            static PropertyDeclarationSyntax OriginalPropertyDeclaration(HookGeneratorContext generationContext)
            {
                var delegateInvokeMethod = generationContext.TargetDelegateType.DelegateInvokeMethod;
                Debug.Assert(delegateInvokeMethod is not null);
                return PropertyDeclaration(IdentifierName(generationContext.TargetDelegateType.ToString()), "Original")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .WithExpressionBody(ArrowExpressionClause(BinaryExpression(SyntaxKind.CoalesceExpression,
                        ConditionalAccessExpression(IdentifierName("_hook"),
                            MemberBindingExpression(IdentifierName("Original"))),
                        LiteralExpression(SyntaxKind.DefaultLiteralExpression))))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }

            static MethodDeclarationSyntax EnableMethodDeclaration(HookGeneratorContext generatorContext)
            {
                return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Enable")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddBodyStatements(IfStatement(
                            InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("global::MinHook.Hook"), IdentifierName("CheckLibraryLoaded")))
                                .AddArgumentListArguments(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                    Literal(generatorContext.ModuleName)))), Block().AddStatements(ExpressionStatement(
                                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                        IdentifierName("_hook"), InvocationExpression(MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName("global::MinHook.Hook"),
                                                GenericName("Create")
                                                    .AddTypeArgumentListArguments(
                                                        IdentifierName(generatorContext.TargetDelegateType.Name))))
                                            .AddArgumentListArguments(
                                                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                                    Literal(generatorContext.ModuleName))),
                                                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                                    Literal(generatorContext.FunctionName))),
                                                Argument(IdentifierName("Detour"))))))
                                .AddStatements(ExpressionStatement(InvocationExpression(MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_hook"),
                                    IdentifierName("Enable"))))))
                        .WithElse(ElseClause(Block()
                            .AddStatements(ExpressionStatement(AssignmentExpression(SyntaxKind.AddAssignmentExpression,
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("global::MinHook.LibraryLoadingMonitor"),
                                    IdentifierName("LibraryLoaded")), IdentifierName("LazyEnable"))))
                            .AddStatements(IfStatement(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("global::MinHook.LibraryLoadingMonitor"), IdentifierName("Enabled")),
                                ExpressionStatement(InvocationExpression(MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("global::MinHook.LibraryLoadingMonitor"),
                                    IdentifierName("Enable")))))))));
            }

            static MethodDeclarationSyntax LazyEnableMethodDeclaration(HookGeneratorContext generatorContext)
            {
                return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "LazyEnable")
                    .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("sender")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
                        Parameter(Identifier("args"))
                            .WithType(IdentifierName("global::MinHook.LibraryLoadingMonitor.LibraryLoadedEventArgs")))
                    .AddBodyStatements(IfStatement(
                        InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("global::MinHook.Hook"), IdentifierName("CheckLibraryLoaded")))
                            .AddArgumentListArguments(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                Literal(generatorContext.ModuleName)))), Block().AddStatements(ExpressionStatement(
                                AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                    IdentifierName("_hook"), InvocationExpression(MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("global::MinHook.Hook"),
                                            GenericName("Create")
                                                .AddTypeArgumentListArguments(
                                                    IdentifierName(generatorContext.TargetDelegateType.Name))))
                                        .AddArgumentListArguments(
                                            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                                Literal(generatorContext.ModuleName))),
                                            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                                Literal(generatorContext.FunctionName))),
                                            Argument(IdentifierName("Detour"))))))
                            .AddStatements(ExpressionStatement(InvocationExpression(MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_hook"),
                                IdentifierName("Enable")))))));
            }

            static MethodDeclarationSyntax DisableMethodDeclaration()
            {
                return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Disable")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddBodyStatements(ExpressionStatement(InvocationExpression(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_hook"),
                        IdentifierName("Disable")))))
                    .AddBodyStatements(ExpressionStatement(AssignmentExpression(SyntaxKind.SubtractAssignmentExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("global::MinHook.LibraryLoadingMonitor"),
                            IdentifierName("LibraryLoaded")), IdentifierName("LazyEnable"))));
            }
        }


    }


}
