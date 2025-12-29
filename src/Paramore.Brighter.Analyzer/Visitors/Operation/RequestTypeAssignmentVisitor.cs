// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Paramore.Brighter.Analyzer.Analyzers;
using Paramore.Brighter.Analyzer.Vistiors.Symbol;


namespace Paramore.Brighter.Analyzer.Vistiors.Operation
{
    public class RequestTypeAssignmentVisitor : OperationWalker
    {
        public bool IsAccepted { get; private set; }
        public bool IsRequestTypeAssigned { get; private set; }
        public string PublicationName { get; private set; }
        public bool IsNotTypeOfIRequest { get; private set; }
        public Location TypeOfLocation { get; private set; }
        public string TypeOfName { get; private set; }

        public override void VisitObjectCreation(IObjectCreationOperation operation)
        {
            if (operation.Type!.Accept(new ChildOfVisitor(BrighterAnalyzerGlobals.PublicationClassName, BrighterAnalyzerGlobals.BrighterAssembly)))
            {
                PublicationName = operation.Type.Name;
                IsAccepted = true;
                base.VisitObjectCreation(operation);
            }
        }
        public override void VisitPropertyReference(IPropertyReferenceOperation operation)
        {
            if (operation.Property.Name == BrighterAnalyzerGlobals.RequestTypeProperty)
            {
                IsRequestTypeAssigned = true;
                base.VisitPropertyReference(operation);
            }
        }
        public override void VisitTypeOf(ITypeOfOperation operation)
        {
            if (!operation.TypeOperand.AllInterfaces.Any(i => i.Name == BrighterAnalyzerGlobals.IRequestInterface && i.ContainingAssembly.Name == BrighterAnalyzerGlobals.BrighterAssembly))
            {
                TypeOfLocation = operation.Syntax.GetLocation();
                TypeOfName = operation.TypeOperand.Name;
                IsNotTypeOfIRequest = true;
            }
            base.VisitTypeOf(operation);
        }
    }
}
