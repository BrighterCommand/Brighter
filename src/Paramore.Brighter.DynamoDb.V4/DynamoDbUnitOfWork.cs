#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.DynamoDb.V4;

public class DynamoDbUnitOfWork(IAmazonDynamoDB dynamoDb) : IAmADynamoDbTransactionProvider, IDisposable
{
    private TransactWriteItemsRequest? _tx;
        
    /// <summary>
    /// The AWS client for dynamoDb
    /// </summary>
    public IAmazonDynamoDB DynamoDb { get; } = dynamoDb;

    /// <summary>
    /// The response for the last transaction commit
    /// </summary>
    public TransactWriteItemsResponse? LastResponse { get; set; }

    public void Close()
    {
        _tx = null;
    }

    /// <summary>
    /// Commit a transaction, performing all associated write actions
    /// Will block thread and use second thread for callback
    /// </summary>
    public void Commit() => BrighterAsyncContext.Run(async () => await CommitAsync());
        
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
    [MemberNotNullWhen(true, nameof(_tx))]
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
