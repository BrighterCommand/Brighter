#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.TestDoubles;

internal sealed class KafkaDisposalTestHost : IDisposable, IAsyncDisposable
{
    private readonly KafkaMessageProducer _producer;
    private readonly RoutingKey _topic = new(Guid.NewGuid().ToString());
    private bool _disposed;

    public KafkaMessageConsumer Consumer { get; }

    public string GroupId { get; } = Guid.NewGuid().ToString();

    public KafkaDisposalTestHost(TimeProvider? timeProvider = null)
    {
        var configuration = new KafkaMessagingGatewayConfiguration
        {
            Name = "consumer-disposal",
            BootStrapServers = ["localhost:9092"]
        };
        _producer = new KafkaMessageProducer(configuration, new KafkaPublication
        {
            Topic = _topic,
            NumPartitions = 1,
            ReplicationFactor = 1,
            MessageTimeoutMs = 2000,
            RequestTimeoutMs = 2000,
            MakeChannels = OnMissingChannel.Create
        });
        _producer.Init();

        try
        {
            // Keep offsets pending until shutdown, regardless of how long test setup takes.
            Consumer = new KafkaMessageConsumer(configuration, _topic, GroupId,
                numPartitions: 1, replicationFactor: 1, makeChannels: OnMissingChannel.Create,
                sweepUncommittedOffsetsInterval: Timeout.InfiniteTimeSpan,
                deadLetterRoutingKey: new RoutingKey(_topic.Value + ".dead-letter"),
                invalidMessageRoutingKey: new RoutingKey(_topic.Value + ".invalid"), timeProvider: timeProvider);
        }
        catch
        {
            _producer.Dispose();
            throw;
        }
    }

    public async Task<Message> ReceiveMessageAsync()
    {
        var id = Guid.NewGuid().ToString();
        var message = new Message(new MessageHeader(id, _topic, MessageType.MT_EVENT),
            new MessageBody("consumer-disposal"));
        await _producer.SendAsync(message);
        _producer.Flush();

        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var received = await Consumer.ReceiveAsync(TimeSpan.FromMilliseconds(250));
            foreach (var candidate in received)
            {
                if (candidate.Id == message.Id)
                    return candidate;
            }
        }

        throw new TimeoutException("Kafka did not deliver the disposal test message within 15 seconds.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            Consumer.Dispose();
        }
        finally
        {
            _producer.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            await Consumer.DisposeAsync();
        }
        finally
        {
            await _producer.DisposeAsync();
        }
    }
}
