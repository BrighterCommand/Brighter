using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb
{
    public interface IDynamoDbClientProvider
    {
        /// <summary>
        /// The AWS client for dynamoDb
        /// </summary>
       IAmazonDynamoDB DynamoDb { get; }
       
       /// <summary>
       /// Begin a transaction if one has not been started, otherwise return the extant transaction
       /// </summary>
       /// <returns></returns>
       TransactWriteItemsRequest BeginOrGetTransaction();
       
       /// <summary>
       /// Commit a transaction, performing all associated write actions
       /// </summary>
       /// <returns>A response indicating the status of the transaction</returns>
       TransactWriteItemsResponse Commit();
       
       /// <summary>
       /// Commit a transaction, performing all associated write actions
       /// </summary>
       /// <param name="ct">The cancellation token for the task</param>
       /// <returns>A response indicating the status of the transaction</returns>
       Task<TransactWriteItemsResponse> CommitAsync(CancellationToken ct);
       
       /// <summary>
       /// Is there an existing transaction
       /// </summary>
       /// <returns></returns>
       bool HasTransaction();

       /// <summary>
       /// Clear any transaction
       /// </summary>
       void Rollback();
    }
}
                                                                        
