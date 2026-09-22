#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

[Trait("Category", "RocketMQ")]
public class WhenPostingAMessageWithPartitionKeyViaTheMessagingGatewayShouldBeReceived : IDisposable
{
    private readonly IAmAMessageGatewayReactorProvider _messageGatewayProvider;
    private readonly IAmAMessageBuilder _messageBuilder;
    private readonly IAmAMessageAssertion _messageAssertion;

    private List<Message> _sentMessages = [];

    private Paramore.Brighter.MessagingGateway.RocketMQ.RocketSubscription? _subscription;
    private Paramore.Brighter.MessagingGateway.RocketMQ.RocketMqPublication? _publication;

    private IAmAMessageProducerSync? _producer;
    private IAmAChannelSync? _channel;

    public WhenPostingAMessageWithPartitionKeyViaTheMessagingGatewayShouldBeReceived()
    {
        _messageGatewayProvider =
            new Paramore.Brighter.RocketMQ.Tests.MessagingGateway.RocketMqMessageGatewayProvider();
        _messageBuilder = new DefaultMessageBuilder();
        _messageAssertion = new RocketMqMessageAssertion();
    }

    public void Dispose()
    {
        _messageGatewayProvider.CleanUp(_producer, _channel, _sentMessages);
    }

    [Fact]
    public void When_posting_a_message_with_partition_key_via_the_messaging_gateway_should_be_received()
    {
        // Arrange
        _publication = _messageGatewayProvider.CreatePublication(
            _messageGatewayProvider.GetOrCreateRoutingKey()
        );
        _subscription = _messageGatewayProvider.CreateSubscription(
            _publication.Topic!,
            _messageGatewayProvider.GetOrCreateChannelName(),
            OnMissingChannel.Create
        );

        _producer = _messageGatewayProvider.CreateProducer(_publication);
        _channel = _messageGatewayProvider.CreateChannel(_subscription);

        var message = _messageBuilder
            .SetTopic(_publication.Topic!)
            .SetPartitionKey(new PartitionKey(Uuid.NewAsString()))
            .Build();
        _sentMessages.Add(message);

        // Act
        _producer.Send(message);

        Thread.Sleep(5000);

        var received = _channel.Receive(null);

        // Assert
        Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);
        _messageAssertion.Assert(message, received);
    }
}
