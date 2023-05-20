using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb
{
    public interface IDynamoDbClientProvider
    {
        /// <summary>
        /// Commits the transaction to Dynamo
        /// </summary>
        /// <param name="ct">A cancellation token</param>
        /// <returns></returns>
        Task<TransactWriteItemsResponse> CommitAsync(CancellationToken ct = default);
        
        /// <summary>
        /// The AWS client for dynamoDb
        /// </summary>
        IAmazonDynamoDB DynamoDb { get; }

        /// <summary>
        /// The response for the last transaction commit
        /// </summary>
        TransactWriteItemsResponse LastResponse { get; set; }
    }
}
