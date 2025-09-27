using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Google.Cloud.ResourceManager.V3;
using Paramore.Brighter.Observability;

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
    
    /// <summary>
    /// The <see cref="ProjectsClientBuilder"/> configuration
    /// </summary>
    public Action<ProjectsClientBuilder>? ProjectsConfiguration { get; set; }
    
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
                    SubscribeConfiguration?.Invoke(builder);
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
                    SubscribeConfiguration?.Invoke(builder);
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
