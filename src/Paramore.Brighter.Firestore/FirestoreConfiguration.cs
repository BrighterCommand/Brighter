using System;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore.V1;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Firestore;

/// <summary>
/// Provides configuration settings and methods for creating Firestore client instances.
/// This class encapsulates common Firestore setup parameters like project ID, database,
/// collection, and optional settings for credentials and client customization.
/// </summary>
public class FirestoreConfiguration(string projectId, string database)
{
    private static readonly TimeSpan s_maxOutstandingCountTimeout = TimeSpan.FromSeconds(int.MaxValue / 1000);
    private TimeSpan? _outstandingCountTimeout;

    /// <summary>
    /// Gets the Google Cloud Project ID.
    /// </summary>
    public string ProjectId { get; } = projectId;
    
    /// <summary>
    /// Gets the Firestore database ID (e.g., "(default)").
    /// </summary>
    public string Database { get; } = database;
    
    /// <summary>
    /// Gets the default inbox Firestore collection.
    /// </summary>
    public FirestoreCollection? Inbox { get; set; }
    
    /// <summary>
    /// Gets the default outbox Firestore collection.
    /// </summary>
    public FirestoreCollection? Outbox { get; set; }
    
    /// <summary>
    /// Gets the default locking Firestore collection.
    /// </summary>
    public FirestoreCollection? Locking { get; set; }
    
    /// <summary>
    /// Gets the full path to the Firestore database.
    /// </summary>
    public string DatabasePath => $"projects/{ProjectId}/databases/{Database}";
    
    /// <summary>
    /// Gets or sets the <see cref="TimeProvider"/> to use for timestamp generation.
    /// Defaults to <see cref="TimeProvider.System"/>.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    
    /// <summary>
    /// Gets or sets the Google credential to use for authentication.
    /// If not set, Application Default Credentials will be used.
    /// </summary>
    public ICredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets the Instrumentation's use for tracing. 
    /// </summary>
    public InstrumentationOptions Instrumentation { get; set; } = InstrumentationOptions.All;
    
    /// <summary>
    /// Gets or sets an action to configure the <see cref="FirestoreClientBuilder"/>
    /// before building the <see cref="FirestoreClient"/>. This allows for advanced
    /// customization of the client.
    /// </summary>
    public Action<FirestoreClientBuilder>? Configure { get; set; }

    /// <summary>
    /// Gets or sets how long the outbox's outstanding message count may run before it gives up.
    /// </summary>
    /// <value>The timeout, or <c>null</c> for the default of 15 seconds.</value>
    /// <remarks>
    /// The timeout is the deadline of the count's call, so when it passes the count throws an
    /// <see cref="Grpc.Core.RpcException"/> with <see cref="Grpc.Core.StatusCode.DeadlineExceeded"/>.
    /// For the count, this deadline replaces any expiration set for <c>RunAggregationQuery</c> through
    /// <see cref="Configure"/>. Only the count uses it; reading the outstanding messages is not bounded by it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not greater than zero or is more than 2147483 seconds.</exception>
    public TimeSpan? OutstandingCountTimeout
    {
        get => _outstandingCountTimeout;
        set
        {
            if (value <= TimeSpan.Zero || value > s_maxOutstandingCountTimeout)
                throw new ArgumentOutOfRangeException(nameof(OutstandingCountTimeout), value,
                    $"The outstanding count timeout must be greater than zero and no more than {s_maxOutstandingCountTimeout.TotalSeconds} seconds.");

            _outstandingCountTimeout = value;
        }
    }
    
    /// <summary>
    /// Gets the full path to the default Firestore collection.
    /// </summary>
    /// <param name="collection">The collection name.</param>
    public string GetCollectionPath(string collection) => $"{DatabasePath}/documents/{collection}";

    /// <summary>
    /// Gets a document name by id
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="collection">The collection name.</param>
    /// <returns>returns the document name</returns>
    public string GetDocumentName(string collection, Id id) => GetDocumentName(collection, id.Value);

    /// <summary>
    /// Gets a document name by id
    /// </summary>
    /// <param name="collection">The collection name.</param>
    /// <param name="id">The id.</param>
    /// <returns>returns the document name</returns>
    public string GetDocumentName(string collection, string id) => $"{GetCollectionPath(collection)}/{id}";
}
