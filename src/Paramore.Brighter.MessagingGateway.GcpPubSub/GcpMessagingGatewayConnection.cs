using Google.Apis.Auth.OAuth2;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud connection
/// </summary>
public class GcpMessagingGatewayConnection
{
    /// <summary>
    /// The project ID
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The Google Cloud credentials
    /// </summary>
    public ICredential? Credential { get; set; }
}
