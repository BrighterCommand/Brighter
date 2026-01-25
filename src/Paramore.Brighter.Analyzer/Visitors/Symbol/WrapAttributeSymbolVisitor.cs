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
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Paramore.Brighter.Analyzer.Vistiors.Symbol;

namespace Paramore.Brighter.Analyzer.Visitors.Symbol
{
    public class WrapAttributeSymbolVisitor : SymbolVisitor
    {
        private List<(string Name, Location Location)> _wrongWrappedMapToRequest = new();
        public IReadOnlyList<(string Name, Location Location)> WrongWrappedMapToRequest => _wrongWrappedMapToRequest;
        private List<(string Name, Location Location)> _wrongWrappedMapToMessage = new();
        public IReadOnlyList<(string Name, Location Location)> WrongWrappedMapToMessage => _wrongWrappedMapToMessage;
        public override void VisitMethod(IMethodSymbol symbol)
        {
            var isMessageMapperType = symbol.ContainingType.AllInterfaces
                .Any(c => c.Name.StartsWith(BrighterAnalyzerGlobals.MessageMapperInterface)
                         && c.ContainingAssembly.Name.Equals(BrighterAnalyzerGlobals.BrighterAssembly));
            if (!isMessageMapperType)
                return;

            if (symbol.Name.StartsWith(BrighterAnalyzerGlobals.MapToRequest))
            {
                ReportInvalidAttributes(symbol, BrighterAnalyzerGlobals.WrapWithAttribute, _wrongWrappedMapToRequest);
            }
            else if (symbol.Name.StartsWith(BrighterAnalyzerGlobals.MapToMessage))
            {
                ReportInvalidAttributes(symbol, BrighterAnalyzerGlobals.UnwrapWithAttribute, _wrongWrappedMapToMessage);
            }

            base.VisitMethod(symbol);
        }
        private void ReportInvalidAttributes(IMethodSymbol symbol, string invalidAttributeBase, List<(string, Location)> collection)
        {
            var visitor = new ChildOfVisitor(invalidAttributeBase, BrighterAnalyzerGlobals.BrighterAssembly);

            foreach (var attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass?.Accept(visitor) == true)
                {
                    var location = attr.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ?? symbol.Locations[0];
                    collection.Add((attr.AttributeClass.Name, location));
                }
            }
        }
    }
}

