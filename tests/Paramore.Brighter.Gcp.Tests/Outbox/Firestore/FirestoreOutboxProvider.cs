using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore.V1;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Gcp.Tests.Firestore;
using Paramore.Brighter.Gcp.Tests.Outbox.Firestore.Async;
using Paramore.Brighter.Gcp.Tests.Outbox.Firestore.Sync;
using Paramore.Brighter.Outbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Outbox.Firestore;

public class FirestoreOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync
{
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
}
