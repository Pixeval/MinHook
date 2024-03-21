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

            spc.AddSource("StaticLazyHookManager.g.cs",
                StaticLazyHookManagerCompilationUnit(generatorContexts).GetText(Encoding.UTF8));
        }));
    }

    private static CompilationUnitSyntax StaticLazyHookManagerCompilationUnit(
        ImmutableArray<HookGeneratorContext> generatorContexts)
    {
        return CompilationUnit()
            .AddMembers(FileScopedNamespaceDeclaration(IdentifierName("MinHook")))
            .AddMembers(StaticLazyHookManagerClassDeclaration(generatorContexts))
            .NormalizeWhitespace();

        static ClassDeclarationSyntax StaticLazyHookManagerClassDeclaration(
            ImmutableArray<HookGeneratorContext> generatorContexts)
        {
            return ClassDeclaration("StaticLazyHookManager")
                .AddModifiers(Token(SyntaxKind.StaticKeyword))
                .AddMembers(GetProcAddressDegelateDeclaration())
                .AddMembers(HookFieldDeclaration())
                .AddMembers(DetourMethodDeclaration(generatorContexts))
                .AddMembers(EnableMethodDeclaration())
                .AddMembers(DisableMethodDeclaration(generatorContexts));

            static MethodDeclarationSyntax EnableMethodDeclaration()
            {
                return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Enable")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddBodyStatements(ExpressionStatement(InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_hook"),
                            IdentifierName("Enable")))));
            }

            static MethodDeclarationSyntax DisableMethodDeclaration(ImmutableArray<HookGeneratorContext> generatorContexts)
            {
                return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Disable")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(Parameter(Identifier("alsoDisableAllEnabledHooks"))
                        .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword))))
                    .AddBodyStatements(ExpressionStatement(InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_hook"),
                            IdentifierName("Enable")))))
                    .AddBodyStatements(IfStatement(IdentifierName("alsoDisableAllEnabledHooks"),
                        Block().AddStatements(generatorContexts.Select(generatorContext =>
                            IfStatement(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(generatorContext.TypeSymbol.ToString()), IdentifierName("Enabled")),
                                ExpressionStatement(InvocationExpression(MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(generatorContext.TypeSymbol.ToString()),
                                    IdentifierName("Disable")))))).OfType<StatementSyntax>().ToArray())));
            }

            static MethodDeclarationSyntax DetourMethodDeclaration(ImmutableArray<HookGeneratorContext> generatorContexts)
            {
                return MethodDeclaration(IdentifierName("global::System.IntPtr"), "Detour")
                    .AddModifiers(Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("hModule"))
                            .WithType(IdentifierName("global::System.IntPtr")),
                        Parameter(Identifier("lpProcName"))
                            .WithType(IdentifierName("global::System.IntPtr")))
                    .AddBodyStatements(LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("global::System.IntPtr")).AddVariables(
                            VariableDeclarator("target").WithInitializer(EqualsValueClause(
                                InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("_hook"), IdentifierName("Original")))
                                    .AddArgumentListArguments(Argument(IdentifierName("hModule")),
                                        Argument(IdentifierName("lpProcName"))))))))
                    .AddBodyStatements(LocalDeclarationStatement(
                        VariableDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword))).AddVariables(
                            VariableDeclarator("name").WithInitializer(EqualsValueClause(
                                InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("global::System.Runtime.InteropServices.Marshal"), IdentifierName("PtrToStringUTF8")))
                                    .AddArgumentListArguments(Argument(IdentifierName("lpProcName"))))))))
                    .AddBodyStatements(SwitchStatement(IdentifierName("name")).AddSections(generatorContexts
                        .Select(generatorContext =>
                            SwitchSection().AddLabels(CaseSwitchLabel(LiteralExpression(
                                    SyntaxKind.StringLiteralExpression, Literal(generatorContext.FunctionName))))
                                .AddStatements(IfStatement(
                                    PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName(generatorContext.TypeSymbol.ToString()),
                                            IdentifierName("Enabled"))),
                                    Block().AddStatements(ExpressionStatement(
                                        InvocationExpression(MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName(generatorContext.TypeSymbol.ToString()),
                                                IdentifierName("Enable")))
                                            .AddArgumentListArguments(Argument(IdentifierName("target")))))))
                                .AddStatements(BreakStatement()))
                        .ToArray()))
                    .AddBodyStatements(ReturnStatement(IdentifierName("target")));
            }

            static FieldDeclarationSyntax HookFieldDeclaration()
            {
                return FieldDeclaration(VariableDeclaration(GenericName("global::MinHook.Hook")
                            .AddTypeArgumentListArguments(IdentifierName("GetProcAddressDelegate")))
                        .AddVariables(VariableDeclarator("_hook").WithInitializer(EqualsValueClause(
                            InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("global::MinHook.Hook"),
                                    GenericName("Create")
                                        .AddTypeArgumentListArguments(IdentifierName("GetProcAddressDelegate"))))
                                .AddArgumentListArguments(
                                    Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                        Literal("kernel32"))),
                                    Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                        Literal("GetProcAddress"))),
                                    Argument(IdentifierName("Detour")))))))
                    .AddModifiers(Token(SyntaxKind.StaticKeyword));
            }

            static DelegateDeclarationSyntax GetProcAddressDegelateDeclaration()
            {
                return DelegateDeclaration(
                        IdentifierName("global::System.IntPtr"),
                        Identifier("GetProcAddressDelegate"))
                    .WithAttributeLists(
                        SingletonList(
                            AttributeList(
                                SingletonSeparatedList(
                                    Attribute(
                                            IdentifierName("global::System.Runtime.InteropServices.UnmanagedFunctionPointer"))
                                        .WithArgumentList(
                                            AttributeArgumentList(
                                                SeparatedList<AttributeArgumentSyntax>(
                                                    new SyntaxNodeOrToken[]
                                                    {
                                                        AttributeArgument(
                                                            MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                IdentifierName(
                                                                    "global::System.Runtime.InteropServices.CallingConvention"),
                                                                IdentifierName("StdCall"))),
                                                        Token(SyntaxKind.CommaToken), AttributeArgument(
                                                                MemberAccessExpression(
                                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                                    IdentifierName(
                                                                        "global::System.Runtime.InteropServices.CharSet"),
                                                                    IdentifierName("Unicode")))
                                                            .WithNameEquals(
                                                                NameEquals(
                                                                    IdentifierName("CharSet"))),
                                                        Token(SyntaxKind.CommaToken), AttributeArgument(
                                                                LiteralExpression(
                                                                    SyntaxKind.TrueLiteralExpression))
                                                            .WithNameEquals(
                                                                NameEquals(
                                                                    IdentifierName("SetLastError")))
                                                    })))))))
                    .WithModifiers(
                        TokenList(
                            Token(SyntaxKind.PublicKeyword)))
                    .AddParameterListParameters(
                        Parameter(Identifier("hModule"))
                            .WithType(IdentifierName("global::System.IntPtr")),
                        Parameter(Identifier("lpProcName"))
                            .WithType(IdentifierName("global::System.IntPtr")));
            }
        }
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
                    .AddParameterListParameters(Parameter(Identifier("target"))
                        .WithType(IdentifierName("global::System.IntPtr")))
                    .AddBodyStatements(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName("_hook"), InvocationExpression(MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("global::MinHook.Hook"),
                                GenericName("Create")
                                    .AddTypeArgumentListArguments(
                                        IdentifierName(generatorContext.TargetDelegateType.Name))))
                            .AddArgumentListArguments(
                                Argument(IdentifierName("target")),
                                Argument(IdentifierName("Detour"))))))
                    .AddBodyStatements(ExpressionStatement(InvocationExpression(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_hook"),
                        IdentifierName("Enable")))));
            }

            static MethodDeclarationSyntax DisableMethodDeclaration()
            {
                return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Disable")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddBodyStatements(ExpressionStatement(InvocationExpression(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_hook"),
                        IdentifierName("Disable")))));
            }
        }


    }


}
