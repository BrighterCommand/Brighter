using Amazon.IdentityManagement;
using Amazon.Scheduler;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.MessageScheduler.Aws;

public static class AWSClientFactoryExtensions
{
    public static IAmazonScheduler CreateSchedulerClient(this AWSClientFactory factory)
    {
        var config = new AmazonSchedulerConfig { RegionEndpoint = factory.RegionEndpoint };

        factory.ClientConfigAction?.Invoke(config);

        return new AmazonSchedulerClient(factory.Credentials, config);
    }

    public static AmazonIdentityManagementServiceClient CreateIdentityClient(this AWSClientFactory factory)
    {
        var config = new AmazonIdentityManagementServiceConfig { RegionEndpoint = factory.RegionEndpoint };
        factory.ClientConfigAction?.Invoke(config);
        return new AmazonIdentityManagementServiceClient(factory.Credentials, config);
    }
}
