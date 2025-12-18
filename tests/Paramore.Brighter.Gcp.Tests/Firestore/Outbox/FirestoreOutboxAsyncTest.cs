using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore.V1;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Outbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Outbox;

public class FirestoreOutboxAsyncTest : OutboxAsyncTest<FirestoreTransaction>
{
    private FirestoreOutbox? _outbox;
    protected override IAmAnOutboxAsync<Message, FirestoreTransaction> Outbox => _outbox!;

    protected override Task CreateStoreAsync()
    {
        _outbox = new FirestoreOutbox(Configuration.CreateOutbox());
        return Task.CompletedTask;
    }

    protected override async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        return await _outbox!.GetAsync(pageSize: 1_000);
    }

    protected override IAmABoxTransactionProvider<FirestoreTransaction> CreateTransactionProvider()
    {
        return new FirestoreUnitOfWork(Configuration.CreateOutbox());
    }

    protected override async Task DeleteStoreAsync()
    {
       var config = Configuration.CreateOutbox();
        var firestore = await new FirestoreConnectionProvider(config)
            .GetFirestoreClientAsync();
        
        foreach (var command in CreatedMessages)
        {
            try
            {
                await firestore.DeleteDocumentAsync(new DeleteDocumentRequest
                {
                    Name = config.GetDocumentName(config.Outbox!.Name, command.Id)
                });
            }
            catch
            {
                // Ignoring any error during delete, it's not important at this point
            }
        }
    }
}
