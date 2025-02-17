using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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
        throw new ChannelFailureException("Simulated socked exception", new RedisException(SocketException, new SocketException((int) SocketError.AccessDenied)));
    }

    protected override Task<IRedisClientAsync?> GetClientAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new ChannelFailureException("Simulated socked exception", new RedisException(SocketException, new SocketException((int) SocketError.AccessDenied)));
    }
}
