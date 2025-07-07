using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore.V1;

namespace Paramore.Brighter.Outbox.Firestore;

public class FirestoreConfiguration(string projectId, string database, string collection)
{
    public string ProjectId { get; } = projectId;
    public string Database { get; } = database;
    public string Collection { get; } = collection;

    public string DatabasePath => $"project/{ProjectId}/database/{Database}";
    public string CollectionPath => $"{DatabasePath}/documents/{Collection}";
    
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    public ICredential? Credential { get; set; }
    public Action<FirestoreClientBuilder>? Configure { get; set; }

    public FirestoreClient CreateFirestoreClient()
    {
        var builder = new FirestoreClientBuilder
        {
            Credential = Credential,
        };
        
        Configure?.Invoke(builder);
        return builder.Build();
    }
    
    public async Task<FirestoreClient> CreateFirestoreClientAsync(CancellationToken cancellationToken = default)
    {
        var builder = new FirestoreClientBuilder
        {
            Credential = Credential,
        };
        
        Configure?.Invoke(builder);
        return await builder.BuildAsync(cancellationToken);
    }
}
