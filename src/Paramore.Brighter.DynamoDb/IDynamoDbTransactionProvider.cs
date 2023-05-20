using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb
{
    public interface IDynamoDbClientTransactionProvider : IDynamoDbClientProvider, IAmABoxTransactionProvider<TransactWriteItemsRequest>
    {
        
    }
}
