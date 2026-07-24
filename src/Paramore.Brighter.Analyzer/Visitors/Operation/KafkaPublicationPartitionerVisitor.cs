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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Paramore.Brighter.Analyzer.Visitors.Symbol;

namespace Paramore.Brighter.Analyzer.Visitors.Operation;

public class KafkaPublicationPartitionerVisitor : OperationWalker
{
    public bool IsKafkaPublication { get; private set; }
    public bool IsPartitionerAssigned { get; private set; }
    public bool IsConsistentRandom { get; private set; }
    public bool IsConsistent { get; private set; }
    public string PublicationName { get; private set; }

    public override void VisitObjectCreation(IObjectCreationOperation operation)
    {
        if (operation.Type!.Accept(new ChildOfVisitor(BrighterAnalyzerGlobals.KafkaPublicationClassName, BrighterAnalyzerGlobals.KafkaMessagingGatewayAssembly)))
        {
            PublicationName = operation.Type.Name;
            IsKafkaPublication = true;
        }

        // base walks the children (including the initializer), which drives
        // VisitSimpleAssignment for any Partitioner assignment.
        base.VisitObjectCreation(operation);
    }

    public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
    {
        if (operation.Target is IPropertyReferenceOperation propertyReference &&
            propertyReference.Property.Name == BrighterAnalyzerGlobals.PartitionerProperty)
        {
            IsPartitionerAssigned = true;

            switch (GetPartitionerValueName(operation.Value))
            {
                case BrighterAnalyzerGlobals.ConsistentRandomPartitionerValue:
                    IsConsistentRandom = true;
                    break;
                case BrighterAnalyzerGlobals.ConsistentPartitionerValue:
                    IsConsistent = true;
                    break;
            }
        }

        base.VisitSimpleAssignment(operation);
    }

    private static string GetPartitionerValueName(IOperation value)
    {
        // Unwrap an implicit conversion (e.g. enum widening) if present.
        if (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        return value is IFieldReferenceOperation fieldReference ? fieldReference.Field.Name : null;
    }
}
