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

namespace Paramore.Brighter.Analyzer.Visitors.Operation;

public class KafkaPublicationPartitionerVisitor : OperationWalker
{
    private readonly INamedTypeSymbol _kafkaPublicationSymbol;
    private readonly INamedTypeSymbol _partitionerEnumSymbol;

    public KafkaPublicationPartitionerVisitor(INamedTypeSymbol kafkaPublicationSymbol, INamedTypeSymbol partitionerEnumSymbol)
    {
        _kafkaPublicationSymbol = kafkaPublicationSymbol;
        _partitionerEnumSymbol = partitionerEnumSymbol;
    }

    public bool IsPartitionerAssigned { get; private set; }
    public bool IsConsistentRandom { get; private set; }
    public bool IsConsistent { get; private set; }
    public string? PublicationName { get; private set; }
    public Location? PartitionerAssignmentLocation { get; private set; }

    public override void VisitObjectCreation(IObjectCreationOperation operation)
    {
        if (IsKafkaPublicationType(operation.Type, _kafkaPublicationSymbol))
        {
            PublicationName = operation.Type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            // base walks the children (including the initializer), which drives
            // VisitSimpleAssignment for any Partitioner assignment. Only descend
            // when this operation is the KafkaPublication itself; reporting for
            // unrelated object creations would attribute nested publications to
            // the wrong location. Note this also descends into nested object
            // creations, so a nested KafkaPublication carrying its own Partitioner
            // would mark the outer one as assigned too — a contrived edge case
            // accepted for simplicity.
            base.VisitObjectCreation(operation);
        }
    }

    public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
    {
        if (operation.Target is IPropertyReferenceOperation propertyReference &&
            propertyReference.Property.Name == BrighterAnalyzerGlobals.PartitionerProperty &&
            IsKafkaPublicationType(propertyReference.Property.ContainingType, _kafkaPublicationSymbol))
        {
            IsPartitionerAssigned = true;
            PartitionerAssignmentLocation = operation.Syntax.GetLocation();

            switch (GetPartitionerValueName(operation.Value, _partitionerEnumSymbol))
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

    // Type can be null for erroneous code in the IDE; treat it as no match.
    internal static bool IsKafkaPublicationType(ITypeSymbol? type, INamedTypeSymbol kafkaPublicationSymbol)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, kafkaPublicationSymbol))
            {
                return true;
            }
        }

        return false;
    }

    internal static string? GetPartitionerValueName(IOperation value, INamedTypeSymbol partitionerEnumSymbol)
    {
        // Unwrap an implicit conversion (e.g. enum widening) if present.
        if (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        // Only fields of the Kafka Partitioner enum itself count; a user field
        // that merely shares a member name (e.g. `Defaults.Consistent`) must not
        // be treated as the enum value.
        return value is IFieldReferenceOperation fieldReference &&
               SymbolEqualityComparer.Default.Equals(fieldReference.Field.ContainingType, partitionerEnumSymbol)
            ? fieldReference.Field.Name
            : null;
    }
}
