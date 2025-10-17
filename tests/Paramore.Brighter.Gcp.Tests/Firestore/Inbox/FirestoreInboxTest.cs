using Google.Cloud.Firestore.V1;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Inbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Inbox;

public class FirestoreInboxTest : InboxTests
{
    private FirestoreInbox? _inbox;
    protected override IAmAnInboxSync Inbox => _inbox!;

    protected override void CreateStore()
    {
        _inbox = new FirestoreInbox(Configuration.CreateInbox());
    }

    protected override void DeleteStore()
    {
        var config = Configuration.CreateInbox();
        var firestore = new FirestoreConnectionProvider(config).GetFirestoreClient();
        
        foreach (var command in CreatedCommands)
        {
            firestore.DeleteDocument(new DeleteDocumentRequest
            {
                Name = $"{config.DatabasePath}/documents/{command.Id}"
            });
        }
    }
}
