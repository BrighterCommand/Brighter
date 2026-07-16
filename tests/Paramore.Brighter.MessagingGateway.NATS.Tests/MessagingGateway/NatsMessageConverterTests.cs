#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Text;
using NATS.Client.Core;
using Paramore.Brighter.MessagingGateway.NATS;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.MessagingGateway.NATS.Tests.MessagingGateway;

public class NatsMessageConverterTests
{
    [Fact]
    public void When_converting_to_nats_headers_should_include_brighter_headers_and_bag()
    {
        var message = new Message(
            new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("orders"),
                messageType: MessageType.MT_EVENT)
            {
                Bag = { ["custom"] = "value" }
            },
            new MessageBody(Encoding.UTF8.GetBytes("payload")));

        var headers = NatsMessageConverter.ToNatsHeaders(message);

        headers.ShouldContainKey("brighter-message-id");
        headers.ShouldContainKey("brighter-topic");
        headers.ShouldContainKey("custom");
        headers["brighter-topic"].ShouldBe("orders");
        headers["custom"].ShouldBe("value");
    }

    [Fact]
    public void When_converting_body_should_return_byte_array()
    {
        var bytes = Encoding.UTF8.GetBytes("hello");
        var message = new Message(
            new MessageHeader(
                Guid.NewGuid().ToString(),
                new RoutingKey("orders"),
                MessageType.MT_EVENT),
            new MessageBody(bytes));

        var body = NatsMessageConverter.GetBody(message);

        body.ShouldBe(bytes);
    }

    [Fact]
    public void When_converting_from_nats_message_should_map_body_headers_and_topic()
    {
        var messageId = Guid.NewGuid().ToString();
        var natsHeaders = new NatsHeaders
        {
            ["brighter-message-id"] = messageId,
            ["brighter-topic"] = "orders",
            ["brighter-message-type"] = "1",
            ["custom"] = "value"
        };
        var data = Encoding.UTF8.GetBytes("payload");
        var natsMsg = new NatsMsg<byte[]>(
            subject: "orders",
            replyTo: null,
            size: data.Length,
            headers: natsHeaders,
            data: data,
            connection: null!);

        var message = NatsMessageConverter.ToMessage(natsMsg);

        message.Header.MessageId.Value.ShouldBe(messageId);
        message.Header.Topic.Value.ShouldBe("orders");
        message.Header.MessageType.ShouldBe(MessageType.MT_COMMAND);
        message.Header.Bag.ShouldContainKey("custom");
        message.Header.Bag["custom"].ShouldBe("value");
        Encoding.UTF8.GetString(message.Body.Bytes).ShouldBe("payload");
    }

    [Fact]
    public void When_converting_from_nats_message_without_topic_header_should_use_subject()
    {
        var natsMsg = new NatsMsg<byte[]>(
            subject: "events.orders",
            replyTo: null,
            size: 0,
            headers: new NatsHeaders(),
            data: Array.Empty<byte>(),
            connection: null!);

        var message = NatsMessageConverter.ToMessage(natsMsg);

        message.Header.Topic.Value.ShouldBe("events.orders");
    }
}
