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

#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

[Trait("Category", "MSSQL")]
[Collection("MSSQLMessagingGateway")]
public class MsSqlCompetingReceiversTests : IDisposable
{
    private readonly RelationalDatabaseConfiguration _configuration = new MsSqlTestHelper().QueueConfiguration;
    private readonly RoutingKey _topic = new("shared.topic");
    private readonly TimeSpan _receiveTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    public void When_receivers_share_a_topic_and_channel_should_deliver_each_message_to_only_one_receiver()
    {
        //Arrange
        var factory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        using var first = factory.CreateSyncChannel(CreateSubscription("first", MessagePumpType.Reactor));
        using var second = factory.CreateSyncChannel(CreateSubscription("second", MessagePumpType.Reactor));
        using var producer = (IAmAMessageProducerSync)new MsSqlMessageProducerFactory(
            _configuration, [new Publication { Topic = _topic }]).Create().Single().Value;
        var firstMessage = CreateMessage("first message");
        var secondMessage = CreateMessage("second message");
        producer.Send(firstMessage);
        producer.Send(secondMessage);

        //Act
        var firstDelivery = first.Receive(_receiveTimeout);
        var secondDelivery = second.Receive(_receiveTimeout);

        //Assert
        Assert.Equal(firstMessage.Id, firstDelivery.Id);
        Assert.Equal(secondMessage.Id, secondDelivery.Id);
        Assert.True(first.Receive(_receiveTimeout).IsEmpty);
        Assert.True(second.Receive(_receiveTimeout).IsEmpty);
    }

    [Fact]
    public async Task When_receivers_share_a_topic_and_channel_should_deliver_each_message_to_only_one_receiver_async()
    {
        //Arrange
        var factory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        using var first = await factory.CreateAsyncChannelAsync(CreateSubscription("first", MessagePumpType.Proactor));
        using var second = await factory.CreateAsyncChannelAsync(CreateSubscription("second", MessagePumpType.Proactor));
        await using var producer = (IAmAMessageProducerAsync)(await new MsSqlMessageProducerFactory(
            _configuration, [new Publication { Topic = _topic }]).CreateAsync()).Single().Value;
        var firstMessage = CreateMessage("first message");
        var secondMessage = CreateMessage("second message");
        await producer.SendAsync(firstMessage);
        await producer.SendAsync(secondMessage);

        //Act
        var firstDelivery = await first.ReceiveAsync(_receiveTimeout);
        var secondDelivery = await second.ReceiveAsync(_receiveTimeout);

        //Assert
        Assert.Equal(firstMessage.Id, firstDelivery.Id);
        Assert.Equal(secondMessage.Id, secondDelivery.Id);
        Assert.True((await first.ReceiveAsync(_receiveTimeout)).IsEmpty);
        Assert.True((await second.ReceiveAsync(_receiveTimeout)).IsEmpty);
    }

    private MsSqlSubscription CreateSubscription(string name, MessagePumpType pumpType) =>
        new(new SubscriptionName(name), new ChannelName(_topic.Value), _topic,
            requestType: typeof(Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand), bufferSize: 1, messagePumpType: pumpType);

    private Message CreateMessage(string body) =>
        new(new MessageHeader(Id.Random(), _topic, MessageType.MT_EVENT), new MessageBody(body));

    public void Dispose() =>
        Configuration.DeleteTable(_configuration.ConnectionString, $"[{_configuration.QueueStoreTable}]");
}
