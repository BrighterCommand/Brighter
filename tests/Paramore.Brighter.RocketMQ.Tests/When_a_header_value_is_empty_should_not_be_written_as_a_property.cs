#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests;

/// <summary>
/// RocketMQ's <c>AddProperty</c> rejects an empty value with
/// <see cref="ArgumentException"/> ("value should not be null or white space"), so an optional
/// header that simply is not set cannot be written through - it has to be omitted.
/// </summary>
/// <remarks>
/// <para>
/// Baggage is the case that bit: it is empty on almost every message, and the producer wrote it
/// unconditionally, so <em>every</em> send through this gateway threw before reaching the broker.
/// </para>
/// <para>
/// Exercised without a broker, through <see cref="RocketMqMessagePublisher"/> - the public seam
/// that says what this gateway would put on the wire, the same shape as
/// <c>MqttMessagePublisher.CreateMqttMessage</c>. That matters here more than elsewhere:
/// <c>rocketmq-ci</c> is commented out in <c>ci.yml</c>, so the generated RocketMQ suite never
/// executes and nothing else in this repository would notice the guards being removed.
/// </para>
/// </remarks>
// "RocketMQ" is the trait every test in this project carries, broker-dependent or not, so it
// cannot select this class. The second value can: rocketmq-ci is commented out in ci.yml, and
// without a selector for the broker-free tests there is no way to run these in CI without also
// running the 47 that need a broker. Any future broker-free RocketMQ test should carry it too.
[Trait("Category", "RocketMQ")]
[Trait("Category", "RocketMQBrokerFree")]
public class RocketMqEmptyHeaderPropertyTests
{
    private static RocketMqPublication APublication() => new()
    {
        Topic = new RoutingKey("conformance-probe")
    };

    private static MessageHeader AHeader() => new(
        messageId: Guid.NewGuid().ToString(),
        topic: new RoutingKey("conformance-probe"),
        messageType: MessageType.MT_EVENT);

    private static Org.Apache.Rocketmq.Message Publish(MessageHeader header) =>
        RocketMqMessagePublisher.CreateRocketMqMessage(
            new Message(header, new MessageBody("{}")),
            APublication(),
            delay: null,
            TimeProvider.System);

    [Fact]
    public void When_baggage_is_empty_should_not_write_the_property()
    {
        // Arrange - a header with no baggage set, which is the common case
        var header = AHeader();
        Assert.Equal(string.Empty, header.Baggage.ToString());

        // Act - would throw if the empty value were handed to AddProperty
        var exception = Record.Exception(() => Publish(header));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void When_baggage_is_set_should_write_the_property()
    {
        // Arrange - non-vacuity: omitting the property must be the empty case only, not always
        var header = AHeader();
        header.Baggage.LoadBaggage("key=value");

        // Act
        var published = Publish(header);

        // Assert
        Assert.Contains(HeaderNames.Baggage, published.Properties.Keys);
    }

    [Fact]
    public void When_the_broker_rejects_an_empty_property_value_should_be_the_reason_for_the_guard()
    {
        // Arrange / Act / Assert - characterises the SDK behaviour the guards exist for, so that a
        // future SDK version quietly accepting empty values shows up here rather than as an
        // unexplained pile of now-pointless conditionals.
        var builder = new Org.Apache.Rocketmq.Message.Builder()
            .SetTopic("conformance-probe")
            .SetBody([1]);

        Assert.Throws<ArgumentException>(() => builder.AddProperty("a-key", string.Empty));
    }
}
