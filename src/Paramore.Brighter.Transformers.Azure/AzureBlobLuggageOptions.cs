using Azure.Core;
using Azure.Storage.Blobs.Models;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transformers.Azure;

/// <summary>
/// Provides configuration options for the <see cref="AzureBlobLuggageStore"/>.
/// These options define how to connect to and interact with an Azure Blob Storage container
/// for the Brighter Claim Check pattern.
/// </summary>
/// <remarks>
/// This class extends <see cref="StorageOptions"/> to include Azure Blob Storage-specific
/// connection and operational settings. You must provide a valid combination of properties
/// to enable the <see cref="AzureBlobLuggageStore"/> to connect successfully:
/// <list type="bullet">
///     <item>
///         <term><see cref="ContainerUri"/> and <see cref="Credential"/></term>
///         <description>
///             Recommended for production. Use with Azure Identity (<see cref="TokenCredential"/>)
///             for secure, passwordless authentication.
///         </description>
///     </item>
///     <item>
///         <term><see cref="ConnectionString"/> and <see cref="ContainerName"/></term>
///         <description>
///             Suitable for development or when using shared access signatures (SAS) or
///             account keys via a connection string.
///         </description>
///     </item>
/// </list>
/// If neither of these valid combinations is provided, the <see cref="AzureBlobLuggageStore"/>
/// constructor will throw an <see cref="ArgumentException"/>.
/// </remarks>
public class AzureBlobLuggageOptions : StorageOptions
{
    /// <summary>
    /// Gets or sets the absolute URI to the Azure Blob container.
    /// This should be used in conjunction with <see cref="Credential"/> for authentication.
    /// </summary>
    /// <example>
    /// <c>new Uri("https://[accountname].blob.core.windows.net/[containername]")</c>
    /// </example>
    public Uri? ContainerUri { get; set; }

    /// <summary>
    /// Gets or sets the Azure <see cref="TokenCredential"/> to use for authenticating
    /// requests to the Azure Blob Storage service.
    /// This is the preferred method for secure authentication using Azure Active Directory
    /// (Microsoft Entra ID) and Managed Identities.
    /// </summary>
    public TokenCredential? Credential { get; set; } 
    
    /// <summary>
    /// Gets or sets the name of the Azure Blob container to use.
    /// This should be used in conjunction with <see cref="ConnectionString"/>.
    /// Container names must be lowercase and conform to Azure Blob naming rules.
    /// </summary>
    public string? ContainerName { get; set; } 
    
    /// <summary>
    /// Gets or sets the connection string for the Azure Storage account.
    /// This should be used in conjunction with <see cref="ContainerName"/>.
    /// </summary>
    /// <example>
    /// <c>"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"</c>
    /// </example>
    public string? ConnectionString { get; set; }
    
    /// <summary>
    /// Gets or sets a dictionary of key-value pairs that will be applied as
    /// metadata to the blobs stored in Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// This metadata is custom user-defined data associated with the blob
    /// and is stored as name-value pairs.
    /// </remarks>
    public Dictionary<string, string>? Metadata { get; set; }
    
    /// <summary>
    /// Gets or sets options for client-provided encryption scope.
    /// This allows for data at rest encryption using customer-managed keys (CMK)
    /// through Azure Storage encryption scopes.
    /// </summary>
    public BlobContainerEncryptionScopeOptions? EncryptionScopeOptions { get; set; }
}
