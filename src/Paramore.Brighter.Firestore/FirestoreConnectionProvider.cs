using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore.V1;

namespace Paramore.Brighter.Firestore;

public class FirestoreConnectionProvider(FirestoreConfiguration configuration) : IAmAFirestoreConnectionProvider
{
    private FirestoreClient? _firestoreClient;
    
    /// <inheritdoc />
    public FirestoreClient GetFirestoreClient()
    {
        if (_firestoreClient != null)
        {
            return _firestoreClient;
        }
        
        _firestoreClient = GetFirestoreClient();
        var builder = new FirestoreClientBuilder { Credential = configuration.Credential };
        configuration.Configure?.Invoke(builder);
        return _firestoreClient = builder.Build();
    }

    /// <inheritdoc />
    public async Task<FirestoreClient> GetFirestoreClientAsync(CancellationToken cancellationToken = default)
    {
        if (_firestoreClient != null)
        {
            return _firestoreClient;
        }
        
        _firestoreClient = GetFirestoreClient();
        var builder = new FirestoreClientBuilder { Credential = configuration.Credential };
        configuration.Configure?.Invoke(builder);
        return _firestoreClient = await builder.BuildAsync(cancellationToken);
    }
}
