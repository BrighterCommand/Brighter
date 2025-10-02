using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore.V1;

namespace Paramore.Brighter.Firestore;

/// <summary>
/// Defines the contract for a provider that manages and supplies connections
/// to Google Cloud Firestore. This interface extends a general connection
/// provider interface to specify Firestore-specific client retrieval methods.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for handling the lifecycle
/// of <see cref="FirestoreClient"/> instances, including their creation and
/// disposal, ensuring efficient and reliable communication with the Firestore service.
/// </remarks>
public interface IAmAFirestoreConnectionProvider : IAmAConnectionProvider
{
    /// <summary>
    /// Synchronously retrieves a <see cref="FirestoreClient"/> instance connected to the
    /// configured Google Cloud Firestore database.
    /// </summary>
    /// <returns>A <see cref="FirestoreClient"/> instance.</returns>
    FirestoreClient GetFirestoreClient();

    /// <summary>
    /// Asynchronously retrieves a <see cref="FirestoreClient"/> instance connected to the
    /// configured Google Cloud Firestore database.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains
    /// a <see cref="FirestoreClient"/> instance.</returns>
    Task<FirestoreClient> GetFirestoreClientAsync(CancellationToken cancellationToken = default);
}
