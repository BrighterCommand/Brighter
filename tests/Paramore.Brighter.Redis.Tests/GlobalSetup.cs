using System;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.MessagingGateway;
using TUnit.Core;
using TUnit.Core.Helpers;

[assembly: ParallelLimiter<ProcessorCountParallelLimit>]

namespace Paramore.Brighter.Redis.Tests;

public static class GlobalSetup
{
    // Drives a full Send/Receive/Ack cycle against Redis once before any test runs.
    // Without this, the first per-fixture BLPOP races a cold pool connect: under
    // parallel test load, the CancellationToken can fire while Redis has already
    // popped a message to satisfy the BLPOP, and the popped item is silently lost.
    // Warming the global RedisConfig static and the Redis server's accept queue here
    // shrinks that race window enough for tests to run reliably in parallel.
    [Before(Assembly)]
    public static async Task WarmUpRedis()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();
        var topic = new RoutingKey($"warmup-{Guid.NewGuid():N}");
        var queueName = new ChannelName(topic.Value);

        await using var producer = new RedisMessageProducer(configuration, new RedisMessagePublication { Topic = topic });
        await using var consumer = new RedisMessageConsumer(configuration, queueName, topic);

        // Subscribe (SADD) and discard any returned message.
        await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(200));

        // Round-trip a single message so the path is fully primed.
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("warmup"));

        await producer.SendAsync(message);

        var batch = await consumer.ReceiveAsync(TimeSpan.FromSeconds(2));
        if (batch.Length > 0 && batch[0].Header.MessageType != MessageType.MT_NONE)
        {
            await consumer.AcknowledgeAsync(batch[0]);
        }

        await consumer.PurgeAsync();
    }
}
