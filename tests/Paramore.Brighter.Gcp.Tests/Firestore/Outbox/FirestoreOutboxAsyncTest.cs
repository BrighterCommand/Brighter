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
        return await _outbox!.GetAsync();
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
            await firestore.DeleteDocumentAsync(new DeleteDocumentRequest
            {
                Name = $"{config.DatabasePath}/documents/{command.Id}"
            });
        }
    }
}
