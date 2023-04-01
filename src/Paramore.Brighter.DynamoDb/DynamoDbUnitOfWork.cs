using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb
{
    public class DynamoDbUnitOfWork : IDynamoDbClientTransactionProvider, IDisposable
    {
        private TransactWriteItemsRequest _tx;

        /// <summary>
        /// The AWS client for dynamoDb
        /// </summary>
        public IAmazonDynamoDB DynamoDb { get; }

        public DynamoDbUnitOfWork(IAmazonDynamoDB dynamoDb)
        {
            DynamoDb = dynamoDb;
        }

        /// <summary>
        /// Begin a transaction if one has not been started, otherwise return the extant transaction
        /// We populate the TransactItems member with an empty list, so you do not need to create your own list
        /// just add your items to the list
        /// i.e. tx.TransactItems.Add(new TransactWriteItem(...
        /// </summary>
        /// <returns></returns>
        public TransactWriteItemsRequest BeginOrGetTransaction()
        {
            if (HasTransaction())
            {
                return _tx;
            }
            else
            {
                _tx = new TransactWriteItemsRequest();
                _tx.TransactItems = new List<TransactWriteItem>();
                return _tx;
            }
        }

        /// <summary>
        /// Commit a transaction, performing all associated write actions
        /// </summary>
        /// <returns>A response indicating the status of the transaction</returns>
        public TransactWriteItemsResponse Commit()
        {
            if (!HasTransaction())
                throw new InvalidOperationException("No transaction to commit");
            
            return DynamoDb.TransactWriteItemsAsync(_tx).GetAwaiter().GetResult();
        }

       /// <summary>
        /// Commit a transaction, performing all associated write actions
        /// </summary>
        /// <returns>A response indicating the status of the transaction</returns>
        public async Task<TransactWriteItemsResponse> CommitAsync(CancellationToken ct = default(CancellationToken))
        {
             if (!HasTransaction())
                 throw new InvalidOperationException("No transaction to commit");
             
             return await DynamoDb.TransactWriteItemsAsync(_tx, ct);
         }

        /// <summary>
        /// Is there an existing transaction
        /// </summary>
        /// <returns></returns>
        public bool HasTransaction()
        {
            return _tx != null;
        }

        /// <summary>
        /// Clear any transaction
        /// </summary>
        public void Rollback()
        {
            _tx = null;
        }

        /// <summary>
        /// Clear any transaction. Does not kill any client to DynamoDb as we assume that we don't own it.
        /// </summary>
        public void Dispose()
        {
            if (HasTransaction())
                _tx = null;
        }
    }
}
