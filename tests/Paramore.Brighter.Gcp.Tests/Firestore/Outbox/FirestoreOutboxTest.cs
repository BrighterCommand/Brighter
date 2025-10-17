using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore.V1;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Outbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Outbox;

public class FirestoreOutboxTest : OutboxTest<FirestoreTransaction>
{
    private FirestoreOutbox? _outbox;
    protected override IAmAnOutboxSync<Message, FirestoreTransaction> Outbox => _outbox!;

    protected override void CreateStore()
    {
        _outbox = new FirestoreOutbox(Configuration.CreateOutbox());
    }

    protected override IEnumerable<Message> GetAllMessages()
    {
        return _outbox!.Get();
    }

    protected override IAmABoxTransactionProvider<FirestoreTransaction> CreateTransactionProvider()
    {
        return new FirestoreUnitOfWork(Configuration.CreateOutbox());
    }

    protected override void DeleteStore()
    {
       var config = Configuration.CreateOutbox();
        var firestore = new FirestoreConnectionProvider(config)
            .GetFirestoreClient();
        
        foreach (var command in CreatedMessages)
        {
            firestore.DeleteDocument(new DeleteDocumentRequest
            {
                Name = $"{config.DatabasePath}/documents/{command.Id}"
            });
        }
    }
}
