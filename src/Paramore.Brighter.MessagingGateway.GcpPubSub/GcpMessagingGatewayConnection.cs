using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Google.Cloud.ResourceManager.V3;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// A centralized gateway for creating and managing thread-safe, lazily initialized 
/// Google Cloud Pub/Sub and ResourceManager clients.
/// </summary>
/// <remarks>
/// This class is updated to implement IDisposable to ensure that the underlying 
/// gRPC resources used by the PublisherServiceApiClient and SubscriberServiceApiClient
/// are properly released.
/// </remarks>
public class GcpMessagingGatewayConnection
{
    /// <summary>
    /// Gets or sets the Google Cloud Project ID to be used for resources.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the credential used for authenticating API calls.
    /// If null, default credentials will be used.
    /// </summary>
    public ICredential? Credential { get; set; }
    
    /// <summary>
    /// Gets or sets a delegate to configure the <see cref="PublisherServiceApiClientBuilder"/> 
    /// before building the client. Allows for setting custom endpoints or RPC options.
    /// </summary>
    public Action<PublisherServiceApiClientBuilder>? PublishConfiguration { get; set; }
    
    /// <summary>
    /// Gets or sets a delegate to configure the <see cref="SubscriberServiceApiClientBuilder"/> 
    /// before building the client (used for low-level pull). Allows for setting custom endpoints or RPC options.
    /// </summary>
    public Action<SubscriberServiceApiClientBuilder>? PullConfiguration { get; set; }
    
    /// <summary>
    /// Gets or sets a delegate to configure the high-level <see cref="SubscriberClientBuilder"/> 
    /// before building the client (used for streaming pull). Allows for setting stream-specific configurations 
    /// (e.g., flow control or message queue options).
    /// </summary>
    public Action<SubscriberClientBuilder>? StreamConfiguration { get; set; }
    
    /// <summary>
    /// Gets or sets a delegate to configure the <see cref="ProjectsClientBuilder"/> 
    /// before building the client.
    /// </summary>
    public Action<ProjectsClientBuilder>? ProjectsConfiguration { get; set; }

    /// <summary>
    /// Creates a high-level SubscriberClient with specified flow control settings.
    /// The caller is responsible for disposing of the returned client.
    /// </summary>
    /// <returns>A configured <see cref="SubscriberClient"/> instance.</returns>
    public SubscriberClient CreateSubscriberClient(Google.Cloud.PubSub.V1.SubscriptionName subscriptionName, long maxInFlightMessages)
    {
        var builder = new SubscriberClientBuilder
        {
            SubscriptionName = subscriptionName,
            Credential = Credential
        };
        
        StreamConfiguration?.Invoke(builder);
        
        builder.Settings ??= new SubscriberClient.Settings();
        builder.Settings.FlowControlSettings = new FlowControlSettings(
            maxOutstandingElementCount: maxInFlightMessages,
            maxOutstandingByteCount: null);
        
        return builder.Build();
    }
    
    /// <summary>
    /// Asynchronously creates a high-level SubscriberClient with specified flow control settings.
    /// The caller is responsible for disposing of the returned client.
    /// </summary>
    /// <returns>A <see cref="Task"/> returning a configured <see cref="SubscriberClient"/> instance.</returns>
    public async Task<SubscriberClient> CreateSubscriberClientAsync(Google.Cloud.PubSub.V1.SubscriptionName subscriptionName, long maxInFlightMessages)
    {
        var builder = new SubscriberClientBuilder
        {
            SubscriptionName = subscriptionName,
            Credential = Credential
        };
        
        StreamConfiguration?.Invoke(builder);
        builder.Settings ??= new SubscriberClient.Settings();
        builder.Settings.FlowControlSettings = new FlowControlSettings(
            maxOutstandingElementCount: maxInFlightMessages,
            maxOutstandingByteCount: null);
        
        return await builder.BuildAsync();
    }
    
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private PublisherServiceApiClient? _publisherServiceApiClient;
    
    /// <summary>
    /// Creates and returns a thread-safe, lazily initialized <see cref="PublisherServiceApiClient"/>.
    /// The client is created only once on the first call.
    /// </summary>
    /// <returns>A configured <see cref="PublisherServiceApiClient"/> instance.</returns>
    public PublisherServiceApiClient CreatePublisherServiceApiClient()
    {
        if (_publisherServiceApiClient == null)
        {
            _semaphoreSlim.Wait();
            try
            {
                if (_publisherServiceApiClient == null)
                {
                    var builder = new PublisherServiceApiClientBuilder { Credential = Credential };
                    PublishConfiguration?.Invoke(builder);
                    _publisherServiceApiClient = builder.Build();
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        return _publisherServiceApiClient;
    }

    /// <summary>
    /// Asynchronously creates and returns a thread-safe, lazily initialized <see cref="PublisherServiceApiClient"/>.
    /// The client is created only once on the first call.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation. The result is a configured <see cref="PublisherServiceApiClient"/> instance.</returns>
    public async Task<PublisherServiceApiClient> CreatePublisherServiceApiClientAsync()
    {
        if (_publisherServiceApiClient == null)
        {
            await _semaphoreSlim.WaitAsync();
            try
            {

                if (_publisherServiceApiClient == null)
                {
                    var builder = new PublisherServiceApiClientBuilder { Credential = Credential };
                    PublishConfiguration?.Invoke(builder);
                    _publisherServiceApiClient = await builder.BuildAsync();
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
        
        return _publisherServiceApiClient;
    }

    private SubscriberServiceApiClient? _subscriberServiceApiClient;
    
    /// <summary>
    /// Creates and returns a thread-safe, lazily initialized <see cref="SubscriberServiceApiClient"/>.
    /// The client is created only once on the first call.
    /// </summary>
    /// <returns>A configured <see cref="SubscriberServiceApiClient"/> instance.</returns>
    public SubscriberServiceApiClient CreateSubscriberServiceApiClient()
    {
        if (_subscriberServiceApiClient == null)
        {
            _semaphoreSlim.Wait();
            try
            {
                if (_subscriberServiceApiClient == null)
                {
                    var builder = new SubscriberServiceApiClientBuilder { Credential = Credential };
                    PullConfiguration?.Invoke(builder);
                    _subscriberServiceApiClient = builder.Build();
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
        
        return _subscriberServiceApiClient;
    }
    
    /// <summary>
    /// Asynchronously creates and returns a thread-safe, lazily initialized <see cref="SubscriberServiceApiClient"/>.
    /// The client is created only once on the first call.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation. The result is a configured <see cref="SubscriberServiceApiClient"/> instance.</returns>
    public async Task<SubscriberServiceApiClient> CreateSubscriberServiceApiClientAsync()
    {
        
        if (_subscriberServiceApiClient == null)
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                if (_subscriberServiceApiClient == null)
                {
                    var builder = new SubscriberServiceApiClientBuilder { Credential = Credential };
                    PullConfiguration?.Invoke(builder);
                    _subscriberServiceApiClient = await builder.BuildAsync();
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        return _subscriberServiceApiClient;
    }

    private ProjectsClient? _projectsClient;
    
    /// <summary>
    /// Asynchronously creates and returns a thread-safe, lazily initialized <see cref="Google.Cloud.ResourceManager.V3.ProjectsClient"/>.
    /// The client is created only once on the first call.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation. The result is a configured <see cref="ProjectsClient"/> instance.</returns>
    public async Task<ProjectsClient> CreateProjectsClientAsync()
    {
        if (_projectsClient == null)
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                if (_projectsClient == null)
                {
                    var  builder = new ProjectsClientBuilder { Credential = Credential };
                    ProjectsConfiguration?.Invoke(builder);
                    _projectsClient = await builder.BuildAsync();
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        return _projectsClient;
    }
}
