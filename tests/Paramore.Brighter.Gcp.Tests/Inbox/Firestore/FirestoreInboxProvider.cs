using System;
using System.Threading.Tasks;
using Google.Cloud.Firestore.V1;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Gcp.Tests.Firestore;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.Inbox.Firestore.Async;
using Paramore.Brighter.Gcp.Tests.Inbox.Firestore.Sync;
using Paramore.Brighter.Inbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Inbox.Firestore;

public class FirestoreInboxProvider : IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    private readonly FirestoreConfiguration _configuration;

    public FirestoreInboxProvider()
    {
        _configuration = new FirestoreConfiguration(GatewayFactory.GetProjectId(), "brighter-firestore-database")
        {
            Credential = GatewayFactory.GetCredential(),
            Inbox = new FirestoreCollection
            {
                Name = $"inbox-{Uuid.New():N}",
                Ttl = TimeSpan.FromMinutes(5)
            }
        };
    }

    public IAmAnInboxSync CreateInbox()
    {
        return new FirestoreInbox(_configuration);
    }

    public IAmAnInboxAsync CreateInboxAsync()
    {
        return new FirestoreInbox(_configuration);
    }

    public void CreateStore()
    {
    }

    public Task CreateStoreAsync()
    {
        return Task.CompletedTask;
    }

    public void DeleteStore()
    {
        DeleteStoreAsync().GetAwaiter().GetResult();
    }

    public async Task DeleteStoreAsync()
    {
        var firestore = await new FirestoreConnectionProvider(_configuration).GetFirestoreClientAsync();

        var documents = firestore.ListDocumentsAsync(
            new ListDocumentsRequest
            {
                Parent = $"{_configuration.DatabasePath}/documents",
                CollectionId = _configuration.Inbox!.Name,
                PageSize = 1000
            }
        );

        await foreach (var document in documents)
        {
            try
            {
                await firestore.DeleteDocumentAsync(new DeleteDocumentRequest { Name = document.Name });
            }
            catch
            {
                // Ignoring any error during delete, it's not important at this point
            }
        }
    }
}
