using System.Net.Sockets;
using Paramore.Brighter.MessagingGateway.Redis;
using ServiceStack.Redis;

namespace Paramore.Brighter.Redis.Tests.TestDoubles;

public class RedisMessageConsumerSocketErrorOnGetClient(
    RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration,
    ChannelName queueName,
    RoutingKey topic)
    : RedisMessageConsumer(redisMessagingGatewayConfiguration, queueName, topic)
{
    private const string SocketException =
        "localhost:6379";

    protected override IRedisClient GetClient()
    {
        throw new RedisException(SocketException, new SocketException((int) SocketError.AccessDenied));
    }

}
