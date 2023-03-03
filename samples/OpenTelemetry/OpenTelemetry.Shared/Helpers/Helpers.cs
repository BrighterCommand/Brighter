using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ;

namespace OpenTelemetry.Shared.Helpers;

public static class Helpers
{
    public static IAmAProducerRegistry GetProducerRegistry(RmqMessagingGatewayConnection rmqConnection)
    {
        return new RmqProducerRegistryFactory(rmqConnection,
            new RmqPublication[]
            {
                new()
                {
                    MaxOutStandingMessages = 5,
                    MaxOutStandingCheckIntervalMilliSeconds = 500,
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("MyDistributedEvent")
                },
                new()
                {
                    MaxOutStandingMessages = 5,
                    MaxOutStandingCheckIntervalMilliSeconds = 500,
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("ProductUpdatedEvent")
                },
                new()
                {
                    MaxOutStandingMessages = 5,
                    MaxOutStandingCheckIntervalMilliSeconds = 500,
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("UpdateProductCommand")
                }
            }).Create();
    }
}
