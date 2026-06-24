using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace RefStructUsing
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RefStructUsingAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "BGLY0001";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(ReportUndisposedRefStructs, SyntaxKind.LocalDeclarationStatement);
        }


        private static void ReportUndisposedRefStructs(SyntaxNodeAnalysisContext context)
        {
            var decl = (LocalDeclarationStatementSyntax)context.Node;

            // Return early if 'using' is present.
            if (decl.UsingKeyword != default)
                return;

            // Don't warn if it's not initialized.
            if (decl.Declaration.Variables.Any(v => v.Initializer is null))
                return;

            // There may be several variables, since "using S a = new(), ..., b = new();" is valid syntax.
            // However, because of how the 'using' syntax works, adding one 'using' at the decl start is enough,
            // so there's no point iterating through all further declarations.
            var variable = decl.Declaration.Variables.FirstOrDefault();

            if (variable == default)
                return;

            // Since we filtered on LocalDeclarationStatement, the variables here are locals.
            var local = (ILocalSymbol)context.SemanticModel.GetDeclaredSymbol(variable);
            var type = local.Type;

            // Return early if not a ref struct or some form of 'using' is already in use.
            if (local.IsUsing || !type.IsValueType || !type.IsRefLikeType)
                return;

            var disposeMethod = type
                .GetMembers("Dispose")
                .Where(s => s.Kind is SymbolKind.Method)
                .FirstOrDefault(WithValidDisposeSignature);

            if (disposeMethod == default)
                return;

            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), local.Name));
        }


        private static bool WithValidDisposeSignature(ISymbol symbol)
        {
            // A ref struct fits the disposable pattern if it has a method that is
            //  1. accessible (AFAIK that is equivalent to 'public' and 'internal' for ref structs),
            //  2. called "Dispose",
            //  3. parameterless,
            //  4. returns void,
            //  5. not static
            var method = (IMethodSymbol)symbol;

            return (method.DeclaredAccessibility is Accessibility.Public || method.DeclaredAccessibility is Accessibility.Internal) &&
                    method.Parameters.IsEmpty &&
                    method.ReturnsVoid &&
                   !method.IsStatic;
        }
    }
}