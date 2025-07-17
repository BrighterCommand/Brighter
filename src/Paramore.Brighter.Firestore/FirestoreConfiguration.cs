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
public class FirestoreConfiguration(string projectId, string database, string collection)
{
    /// <summary>
    /// Gets the Google Cloud Project ID.
    /// </summary>
    public string ProjectId { get; } = projectId;
    
    /// <summary>
    /// Gets the Firestore database ID (e.g., "(default)").
    /// </summary>
    public string Database { get; } = database;
    
    /// <summary>
    /// Gets the default Firestore collection ID.
    /// </summary>
    public string Collection { get; } = collection;

    /// <summary>
    /// Gets the full path to the Firestore database.
    /// </summary>
    public string DatabasePath => $"project/{ProjectId}/database/{Database}";
    
    /// <summary>
    /// Gets the full path to the default Firestore collection.
    /// </summary>
    public string CollectionPath => $"{DatabasePath}/documents/{Collection}";
    
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
    /// Gets a document name by id
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>returns the document name</returns>
    public string GetDocumentName(Id id) => GetDocumentName(id.Value);
    
    /// <summary>
    /// Gets a document name by id
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>returns the document name</returns>
    public string GetDocumentName(string id) => $"{CollectionPath}/{id}";
}
