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
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("MyDistributedEvent")
                },
                new()
                {
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("ProductUpdatedEvent")
                },
                new()
                {
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create,
                    Topic = new RoutingKey("UpdateProductCommand")
                }
            }).Create();
    }
}
