#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.SqsFifo.Reactor;

[Trait("Category", "Sqs")]
[Collection("SqsFifo")]
public class SqsFifoDeferredOrderingTests : IAsyncLifetime
{
    private readonly SqsFifoMessageGatewayProvider _provider = new();
    private IAmAMessageProducerSync _producer;
    private IAmAChannelSync _channel;
    private readonly List<Message> _messages = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _provider.CleanUp(_producer, _channel, _messages);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task When_deferring_the_head_of_a_fifo_group_should_preserve_order()
    {
        // Arrange
        var publication = _provider.CreatePublication(_provider.GetOrCreateRoutingKey());
        var subscription = _provider.CreateSubscription(publication.Topic!,
            _provider.GetOrCreateChannelName(), OnMissingChannel.Create);
        _producer = _provider.CreateProducer(publication);
        using var pumpChannel = new QuitAfterRequeueChannel(subscription.ChannelName, publication.Topic!,
            new SqsMessageConsumer(GatewayFactory.CreateFactory(), publication.Topic!.Value,
                queueAttributes: publication.QueueAttributes));
        var observerSubscription = _provider.CreateSubscription(publication.Topic!,
            subscription.ChannelName, OnMissingChannel.Create);
        _channel = _provider.CreateChannel(observerSubscription);

        var group = new PartitionKey(Uuid.NewAsString());
        foreach (var id in new[] { Id.Random(), Id.Random(), Id.Random() })
        {
            var message = new FifoMessageBuilder().SetTopic(publication.Topic!)
                .SetPartitionKey(group).SetMessageId(id).SetMessageType(MessageType.MT_COMMAND)
                .SetBody(ConformanceDeferredPump.CommandBody()).Build();
            _messages.Add(message);
            _producer.Send(message);
        }

        ConformanceDeferredPump.ResetDispatchCount();
        var pump = ConformanceDeferredPump.CreateReactor(pumpChannel, -1, TimeSpan.FromSeconds(1));
        pump.RequeueDelay = TimeSpan.FromSeconds(10);

        // Act
        var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);
        var waiting = Stopwatch.StartNew();
        try
        {
            await pumping.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            if (!pumping.IsCompleted)
            {
                pumpChannel.Enqueue(MessageFactory.CreateQuitMessage(subscription.RoutingKey));
                await pumping.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }

        // Assert
        Assert.True(pumpChannel.RequeueSucceeded);
        Assert.Equal(1, ConformanceDeferredPump.GetDispatchCount(ConformanceDeferredPump.KeyOf(_messages[0])));
        Assert.Equal(0, ConformanceDeferredPump.GetDispatchCount(ConformanceDeferredPump.KeyOf(_messages[1])));
        Assert.Equal(0, ConformanceDeferredPump.GetDispatchCount(ConformanceDeferredPump.KeyOf(_messages[2])));
        var beforeDelay = _channel.Receive(TimeSpan.FromSeconds(2));
        Assert.Equal(MessageType.MT_NONE, beforeDelay.Header.MessageType);

        foreach (var expected in _messages)
        {
            var received = new Message();
            waiting.Restart();
            while (received.Header.MessageType == MessageType.MT_NONE && waiting.Elapsed < TimeSpan.FromSeconds(20))
            {
                received = _channel.Receive(TimeSpan.FromSeconds(1));
            }
            Assert.Equal(expected.Id, received.Id);
            _channel.Acknowledge(received);
        }
    }
}
