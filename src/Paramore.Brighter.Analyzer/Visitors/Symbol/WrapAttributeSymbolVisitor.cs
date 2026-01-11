
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
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
            if (IsMessageMapperType)
            {
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
}
