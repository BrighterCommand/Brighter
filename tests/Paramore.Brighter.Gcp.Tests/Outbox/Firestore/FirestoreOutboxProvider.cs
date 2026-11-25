using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore.V1;
using Grpc.Core;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Gcp.Tests.Firestore;
using Paramore.Brighter.Gcp.Tests.Outbox.Firestore.Async;
using Paramore.Brighter.Gcp.Tests.Outbox.Firestore.OutstandingCount;
using Paramore.Brighter.Gcp.Tests.Outbox.Firestore.Sync;
using Paramore.Brighter.Outbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Outbox.Firestore;

public class FirestoreOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync, IAmAnOutstandingCountOutboxProvider
{
    private static readonly Lazy<bool> s_earlierRunsCleared = new(() =>
    {
        new FirestoreOutboxProvider().DeleteOutstandingCountStore();
        return true;
    });

    public IAmAnOutboxSync<Message, FirestoreTransaction> CreateOutbox()
    {
        return new FirestoreOutbox(Configuration.CreateOutbox());
    }

    public IAmAnOutboxAsync<Message, FirestoreTransaction> CreateOutboxAsync()
    {
        return new FirestoreOutbox(Configuration.CreateOutbox());
    }

    public void CreateStore() { }

    public Task CreateStoreAsync()
    {
        return Task.CompletedTask;
    }

    public IAmABoxTransactionProvider<FirestoreTransaction> CreateTransactionProvider()
    {
        return new FirestoreUnitOfWork(Configuration.CreateOutbox());
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        var config = Configuration.CreateOutbox();
        var firestore = new FirestoreConnectionProvider(config).GetFirestoreClient();

        foreach (var message in messages)
        {
            try
            {
                firestore.DeleteDocument(
                    new DeleteDocumentRequest
                    {
                        Name = config.GetDocumentName(config.Outbox!.Name, message.Id),
                    }
                );
            }
            catch
            {
                // Ignoring any error during delete, it's not important at this point
            }
        }
    }

    public async Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        var config = Configuration.CreateOutbox();
        var firestore = new FirestoreConnectionProvider(config).GetFirestoreClient();

        foreach (var message in messages)
        {
            try
            {
                await firestore.DeleteDocumentAsync(
                    new DeleteDocumentRequest
                    {
                        Name = config.GetDocumentName(config.Outbox!.Name, message.Id),
                    }
                );
            }
            catch
            {
                // Ignoring any error during delete, it's not important at this point
            }
        }
    }

    public IEnumerable<Message> GetAllMessages()
    {
        var outbox = new FirestoreOutbox(Configuration.CreateOutbox());
        return outbox.Get(pageSize: 1_000);
    }

    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var outbox = new FirestoreOutbox(Configuration.CreateOutbox());
        return await outbox.GetAsync(pageSize: 1_000);
    }

    public void DeleteOutstandingCountStore()
    {
        try
        {
            var config = Configuration.CreateOutstandingCountOutbox();
            var firestore = new FirestoreConnectionProvider(config).GetFirestoreClient();
            var outbox = new FirestoreOutbox(config);

            for (var messages = outbox.Get(pageSize: 1_000).ToList(); messages.Count > 0; messages = outbox.Get(pageSize: 1_000).ToList())
            {
                foreach (var message in messages)
                {
                    firestore.DeleteDocument(new DeleteDocumentRequest { Name = config.GetDocumentName(config.Outbox!.Name, message.Id) });
                }
            }
        }
        catch (Exception)
        {
        }
    }

    public IAmAnOutboxAsync<Message, FirestoreTransaction> CreateOutstandingCountStore()
    {
        ClearMessagesLeftByEarlierRuns();
        return new FirestoreOutbox(Configuration.CreateOutstandingCountOutbox());
    }

    private static void ClearMessagesLeftByEarlierRuns() => _ = s_earlierRunsCleared.Value;

    public Task<OutstandingCountCancellationSetup<FirestoreTransaction>> PrepareCancellationAsync(TimeSpan holdFor)
    {
        var outbox = new FirestoreOutbox(Configuration.CreateOutstandingCountOutbox());
        return Task.FromResult(new OutstandingCountCancellationSetup<FirestoreTransaction>(
            outbox,
            storeHeld: false,
            clientCancellationException: typeof(RpcException),
            release: () =>
            {
                DeleteOutstandingCountStore();
                return ValueTask.CompletedTask;
            }));
    }
}
