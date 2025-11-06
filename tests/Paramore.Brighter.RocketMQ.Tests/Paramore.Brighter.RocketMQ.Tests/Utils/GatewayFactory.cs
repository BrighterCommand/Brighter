using Org.Apache.Rocketmq;
using Paramore.Brighter.MessagingGateway.RocketMQ;

namespace Paramore.Brighter.RocketMQ.Tests.Utils;

public static class GatewayFactory
{
    public static RocketMessagingGatewayConnection CreateConnection()
    {
        return new RocketMessagingGatewayConnection(new ClientConfig.Builder()
            .SetEndpoints("localhost:8081")
            .EnableSsl(false)
            .SetRequestTimeout(TimeSpan.FromSeconds(10))
            .Build());
    }
}
