using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB.V4;

internal sealed class BrighterDynamoDBFilter
{
    public QueryOperator Operator { get; }
    public IEnumerable<string> Values { get; }

    public BrighterDynamoDBFilter(QueryOperator @operator, IEnumerable<string> values)
        => (Operator, Values) = (@operator, values);
}
