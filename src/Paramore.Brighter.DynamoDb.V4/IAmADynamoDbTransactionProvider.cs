using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb.V4;

public interface IAmADynamoDbTransactionProvider : IAmADynamoDbConnectionProvider, IAmABoxTransactionProvider<TransactWriteItemsRequest>;