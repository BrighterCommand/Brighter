using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Google.Cloud.ResourceManager.V3;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// A gateway to manage Google Cloud Pub/Sub connections and clients.
/// This class wraps the Google PubSub client builders to allow configuration customization and client creation.
/// </summary>
public class GcpMessagingGatewayConnection
{
    /// <summary>
    /// Gets or sets the <see cref="ICredential"/> to use for authentication with Google Cloud Pub/Sub.
    /// If not set, the default Google credential resolution will be used.
    /// </summary>
    public ICredential? Credential { get; set; }
    
    /// <summary>
    /// Gets or sets the Google Cloud project ID. This is required for most operations.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;
    
    /// <summary>
    /// Action to configure the <see cref="PublisherServiceApiClientBuilder"/> used for topic management (create/update/delete topics).
    /// This allows for advanced customization of the underlying gRPC client.
    /// </summary>
    public Action<PublisherServiceApiClientBuilder>? TopicManagerConfiguration { get; set; }
    
    /// <summary>
    /// Action to configure the <see cref="PublisherClientBuilder"/> used to publish messages to a topic.
    /// This allows for advanced customization of the underlying publishing client.
    /// </summary>
    public Action<PublisherClientBuilder>? PublisherConfiguration { get; set; }
    
    /// <summary>
    /// Action to configure the <see cref="SubscriberServiceApiClientBuilder"/> used for pull mode and subscription management (create/update/delete subscription).
    /// This allows for advanced customization of the underlying gRPC client.
    /// </summary>
    public Action<SubscriberServiceApiClientBuilder>? SubscriptionManagerConfiguration { get; set; }
    
    /// <summary>
    /// Action to configure the <see cref="SubscriberClientBuilder"/> used for pull mode message consumption.
    /// This allows for advanced customization of the underlying subscribe client.
    /// </summary>
    public Action<SubscriberClientBuilder>? StreamConfiguration { get; set; }
    
    /// <summary>
    /// Action to configure the <see cref="ProjectsClientBuilder"/> used for managing projects.
    /// This allows for advanced customization of the underlying gRPC client.
    /// </summary>
    public Action<ProjectsClientBuilder>? ProjectsClientConfiguration { get; set; }

    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private PublisherServiceApiClient? _publisherServiceApiClient;
    
    /// <summary>
    /// Creates or returns a cached, thread-safe <see cref="PublisherServiceApiClient"/> for managing topics.
    /// </summary>
    /// <returns>A <see cref="PublisherServiceApiClient"/> instance.</returns>
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
                    TopicManagerConfiguration?.Invoke(builder);
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
    /// Asynchronously creates or returns a cached, thread-safe <see cref="PublisherServiceApiClient"/> for managing topics.
    /// </summary>
    /// <returns>A task that returns a <see cref="PublisherServiceApiClient"/> instance.</returns>
    public async Task<PublisherServiceApiClient> CreatePublisherServiceApiClientAsync(Action<PublisherServiceApiClientBuilder>? configure = null)
    {
        if (_publisherServiceApiClient == null)
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                if (_publisherServiceApiClient == null)
                {
                    var builder = new PublisherServiceApiClientBuilder { Credential = Credential };
                    TopicManagerConfiguration?.Invoke(builder);
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
    /// Creates or returns a cached, thread-safe <see cref="SubscriberServiceApiClient"/> for managing subscriptions.
    /// </summary>
    /// <returns>A <see cref="SubscriberServiceApiClient"/> instance.</returns>
    public SubscriberServiceApiClient GetOrCreateSubscriberServiceApiClient()
    {
        if (_subscriberServiceApiClient == null)
        {
            _semaphoreSlim.Wait();
            try
            {
                if (_subscriberServiceApiClient == null)
                {
                    var builder = new SubscriberServiceApiClientBuilder { Credential = Credential };
                    SubscriptionManagerConfiguration?.Invoke(builder);
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
    /// Asynchronously creates or returns a cached, thread-safe <see cref="SubscriberServiceApiClient"/> for managing subscriptions.
    /// </summary>
    /// <returns>A task that returns a <see cref="SubscriberServiceApiClient"/> instance.</returns>
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
                    SubscriptionManagerConfiguration?.Invoke(builder);
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
    /// Asynchronously creates or returns a cached, thread-safe <see cref="ProjectsClient"/> for project-level operations.
    /// </summary>
    /// <returns>A task that returns a <see cref="ProjectsClient"/> instance.</returns>
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
                    ProjectsClientConfiguration?.Invoke(builder);
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
