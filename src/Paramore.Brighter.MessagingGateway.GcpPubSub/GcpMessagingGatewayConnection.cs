using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;

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
    
    /// <summary>
    /// The <see cref="Google.Cloud.PubSub.V1.PublisherClientBuilder"/> configuration
    /// </summary>
    public Action<PublisherServiceApiClientBuilder>? PublishConfiguration { get; set; }
    
    /// <summary>
    /// The <see cref="SubscriberClientBuilder"/> configuration
    /// </summary>
    public Action<SubscriberServiceApiClientBuilder>? SubscribeConfiguration { get; set; }
}
