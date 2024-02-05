using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb
{
    public class DynamoDbUnitOfWork : IAmADynamoDbTransactionProvider, IDisposable
    {
        private TransactWriteItemsRequest _tx;
        
        /// <summary>
        /// The AWS client for dynamoDb
        /// </summary>
        public IAmazonDynamoDB DynamoDb { get; }
        
        /// <summary>
        /// The response for the last transaction commit
        /// </summary>
        public TransactWriteItemsResponse LastResponse { get; set; }

        public DynamoDbUnitOfWork(IAmazonDynamoDB dynamoDb)
        {
            DynamoDb = dynamoDb;
        }

        public void Close()
        {
            _tx = null;
        }

        /// <summary>
        /// Commit a transaction, performing all associated write actions
        /// </summary>
        public void Commit()
        {
            if (!HasOpenTransaction)
                throw new InvalidOperationException("No transaction to commit");
            
            LastResponse = DynamoDb.TransactWriteItemsAsync(_tx).GetAwaiter().GetResult();

        }
        
        /// <summary>
        /// Commit a transaction, performing all associated write actions
        /// </summary>
        /// <param name="ct">A cancellation token</param>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (!HasOpenTransaction)
                throw new InvalidOperationException("No transaction to commit");
            try
            {
               LastResponse = await DynamoDb.TransactWriteItemsAsync(_tx, ct);
                if (LastResponse.HttpStatusCode != HttpStatusCode.OK)
                    throw new InvalidOperationException($"HTTP error writing to DynamoDb {LastResponse.HttpStatusCode}");
            }
            catch (AmazonDynamoDBException e)
            {
                throw new InvalidOperationException($"HTTP error writing to DynamoDb {e.Message}");
            }
        }


        /// <summary>
        /// Begin a transaction if one has not been started, otherwise return the extant transaction
        /// We populate the TransactItems member with an empty list, so you do not need to create your own list
        /// just add your items to the list
        /// i.e. tx.TransactItems.Add(new TransactWriteItem(...
        /// </summary>
        /// <returns></returns>
        public TransactWriteItemsRequest GetTransaction()
        {
            if (HasOpenTransaction)
            {
                return _tx;
            }

            _tx = new TransactWriteItemsRequest();
            _tx.TransactItems = new List<TransactWriteItem>();
            return _tx;
        }

        public Task<TransactWriteItemsRequest> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<TransactWriteItemsRequest>();
            tcs.SetResult(GetTransaction());
            return tcs.Task;
        }

        /// <summary>
        /// Is there an existing transaction?
        /// </summary>
        /// <returns></returns>
        public bool HasOpenTransaction => _tx != null; 

        /// <summary>
        /// Is there a shared connection, not true, but we do not manage the DynamoDb client
        /// </summary>
        public bool IsSharedConnection => false;

        /// <summary>
        /// Clear any transaction
        /// </summary>
        public void Rollback()
        {
            _tx = null;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback(); 
            return Task.CompletedTask;
        }

        /// <summary>
        /// Clear any transaction. Does not kill any client to DynamoDb as we assume that we don't own it.
        /// </summary>
        public void Dispose()
        {
            if (HasOpenTransaction)
                _tx = null;
        }
    }
}
