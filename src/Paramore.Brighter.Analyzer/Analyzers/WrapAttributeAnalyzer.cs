#region License
/* The MIT License (MIT)
Copyright © 2026 Aboubakr Nasef <aboubakrnasef@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Paramore.Brighter.Analyzer.Visitors.Operation;
using Paramore.Brighter.Analyzer.Visitors.Symbol;

namespace Paramore.Brighter.Analyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WrapAttributeAnalyzer : DiagnosticAnalyzer
    {
        private const string WrapAttributeCategory = "Design";

        public static DiagnosticDescriptor WrapAttributeRule = new DiagnosticDescriptor(
               id: DiagnosticsIds.WrapWithAttribute,
               title: "WrapAttribute",
               messageFormat: $"{{0}} should be applied '{BrighterAnalyzerGlobals.MapToMessage}' Method",
               category: WrapAttributeCategory,
               defaultSeverity: DiagnosticSeverity.Warning,
               isEnabledByDefault: true
         //   helpLinkUri: GetRuleUrl(Rule)
         );
        public static DiagnosticDescriptor UnWrapWithAttributeRule = new DiagnosticDescriptor(
        id: DiagnosticsIds.UnWrapWithAttribute,
        title: "UnWrapWithAttribute",
        messageFormat: $"{{0}} should be applied '{BrighterAnalyzerGlobals.MapToRequest}' Method",
        category: WrapAttributeCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
  //   helpLinkUri: GetRuleUrl(Rule)
  );
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [WrapAttributeRule, UnWrapWithAttributeRule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var visitor = new WrapAttributeSymbolVisitor();
            context.Symbol.Accept(visitor);
            foreach (var item in visitor.WrongWrappedMapToRequest)
            {
                context.ReportDiagnostic(Diagnostic.Create(WrapAttributeRule, item.Location, item.Name));
            }
            foreach (var item in visitor.WrongWrappedMapToMessage)
            {
                context.ReportDiagnostic(Diagnostic.Create(UnWrapWithAttributeRule, item.Location, item.Name));
            }

        }
    }
}
