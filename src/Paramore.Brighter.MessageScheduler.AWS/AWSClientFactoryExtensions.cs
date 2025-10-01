using Amazon.IdentityManagement;
using Amazon.Scheduler;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.MessageScheduler.AWS;

/// <summary>
/// The AWS Client Factory
/// </summary>
public static class AWSClientFactoryExtensions
{
    /// <summary>
    /// Create new instance of <see cref="IAmazonScheduler"/>
    /// </summary>
    /// <param name="factory">The <see cref="AWSClientFactory"/>.</param>
    /// <returns>
    /// Create new instance of <see cref="IAmazonScheduler"/>
    /// </returns>
    public static IAmazonScheduler CreateSchedulerClient(this AWSClientFactory factory)
    {
        var config = new AmazonSchedulerConfig { RegionEndpoint = factory.RegionEndpoint };

        factory.ClientConfigAction?.Invoke(config);

        return new AmazonSchedulerClient(factory.Credentials, config);
    }

    /// <summary>
    /// Create new instance of <see cref="IAmazonIdentityManagementService "/>
    /// </summary>
    /// <param name="factory">The <see cref="AWSClientFactory"/>.</param>
    /// <returns>
    /// Create new instance of <see cref="IAmazonIdentityManagementService "/>
    /// </returns>
    public static IAmazonIdentityManagementService CreateIdentityClient(this AWSClientFactory factory)
    {
        var config = new AmazonIdentityManagementServiceConfig { RegionEndpoint = factory.RegionEndpoint };
        factory.ClientConfigAction?.Invoke(config);
        return new AmazonIdentityManagementServiceClient(factory.Credentials, config);
    }
}
