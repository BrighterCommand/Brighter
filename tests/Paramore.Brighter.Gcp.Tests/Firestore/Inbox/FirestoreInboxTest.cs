using System;
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
            try
            {
                firestore.DeleteDocument(new DeleteDocumentRequest
                {
                    Name = config.GetDocumentName(config.Inbox!.Name, command.Id)
                });
            }
            catch (Exception e)
            {
                // Ignoring any error during delete, it's not important at this point
            }
        }
    }
}
