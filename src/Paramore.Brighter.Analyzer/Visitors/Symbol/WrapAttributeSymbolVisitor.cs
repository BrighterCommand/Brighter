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
        List<(string Name, Location Location)> _wrongWrappedMapToRequest = new();
        public IReadOnlyList<(string Name, Location Location)> WrongWrappedMapToRequest => _wrongWrappedMapToRequest;
        List<(string Name, Location Location)> _wrongWrappedMapToMessage = new();
        public IReadOnlyList<(string Name, Location Location)> WrongWrappedMapToMessage => _wrongWrappedMapToMessage;
        public override void VisitMethod(IMethodSymbol symbol)
        {
            var IsMessageMapperType = symbol.ContainingType.AllInterfaces
                .Any(c => c.Name.StartsWith(BrighterAnalyzerGlobals.MessageMapperInterface)
                         && c.ContainingAssembly.Name.Equals(BrighterAnalyzerGlobals.BrighterAssembly));
            if (!IsMessageMapperType)
                return;

            if (symbol.Name.StartsWith(BrighterAnalyzerGlobals.MapToRequest))
            {
                var childOfVisitor = new ChildOfVisitor(BrighterAnalyzerGlobals.WrapWithAttribute, BrighterAnalyzerGlobals.BrighterAssembly);
                var attributes = symbol.GetAttributes().Where(atr => atr.AttributeClass.Accept(childOfVisitor));
                foreach (var attr in attributes)
                {
                    var loc = attr.ApplicationSyntaxReference.GetSyntax().GetLocation();
                    _wrongWrappedMapToRequest.Add((attr.AttributeClass.Name, loc));
                }
            }
            else if (symbol.Name.StartsWith(BrighterAnalyzerGlobals.MapToMessage))
            {
                var childOfVisitor = new ChildOfVisitor(BrighterAnalyzerGlobals.UnwrapWithAttribute, BrighterAnalyzerGlobals.BrighterAssembly);
                var attributes = symbol.GetAttributes().Where(atr => atr.AttributeClass.Accept(childOfVisitor));
                foreach (var attr in attributes)
                {
                    var loc = attr.ApplicationSyntaxReference.GetSyntax().GetLocation();
                    _wrongWrappedMapToMessage.Add((attr.AttributeClass.Name, loc));
                }
            }

            base.VisitMethod(symbol);
        }
    }
}

