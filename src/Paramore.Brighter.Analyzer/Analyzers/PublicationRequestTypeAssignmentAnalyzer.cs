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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Paramore.Brighter.Analyzer.Visitors.Operation;

namespace Paramore.Brighter.Analyzer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PublicationRequestTypeAssignmentAnalyzer : DiagnosticAnalyzer
    {
        private const string RequestPublicationCategory = "Design";

        public static DiagnosticDescriptor RequestTypeMissingRule = new DiagnosticDescriptor(
               id: DiagnosticsIds.RequestTypeMissing,
               title: "Request Type Missing",
               messageFormat: "RequestType assignment is Missing from {0}",
               category: RequestPublicationCategory,
               defaultSeverity: DiagnosticSeverity.Warning,
               isEnabledByDefault: true
         );
        public static DiagnosticDescriptor WrongRequestTypeRule = new DiagnosticDescriptor(
               id: DiagnosticsIds.WrongRequestType,
               title: "Wrong Request Type",
               messageFormat: "RequestType '{0}' is not child of IRequest",
               category: RequestPublicationCategory,
               defaultSeverity: DiagnosticSeverity.Warning,
               isEnabledByDefault: true
         );
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return [RequestTypeMissingRule, WrongRequestTypeRule]; } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(AnalyzerOperation, OperationKind.ObjectCreation);
        }

        private void AnalyzerOperation(OperationAnalysisContext context)
        {
            var visitor = new RequestTypeAssignmentVisitor();
            context.Operation.Accept(visitor);
            if (visitor.IsPublicationType && !visitor.IsRequestTypeAssigned)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RequestTypeMissingRule,
                    context.Operation.Syntax.GetLocation(),
                    visitor.PublicationName));
            }
            else if (visitor.IsNotTypeOfIRequest)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    WrongRequestTypeRule,
                   visitor.TypeOfLocation,
                   visitor.TypeOfName));
            }
        }
    }
}
