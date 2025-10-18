using System.Threading.Tasks;
using Google.Cloud.Firestore.V1;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Inbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Inbox;

public class FirestoreInboxAsyncTest : InboxAsyncTest
{
    private FirestoreInbox? _inbox;
    protected override IAmAnInboxAsync Inbox => _inbox!;

    protected override Task CreateStoreAsync()
    {
        _inbox = new FirestoreInbox(Configuration.CreateInbox());
        return Task.CompletedTask;
    }

    protected override async Task DeleteStoreAsync()
    {
        var config = Configuration.CreateInbox();
        var firestore = await new FirestoreConnectionProvider(config)
            .GetFirestoreClientAsync();
        
        foreach (var command in CreatedCommands)
        { 
            try
            {
                await firestore.DeleteDocumentAsync(new DeleteDocumentRequest
                {
                    Name = config.GetDocumentName(config.Inbox!.Name, command.Id)
                });
            }
            catch 
            {
                // Ignoring any error during delete, it's not important at this point
            }
        }
    }
}
