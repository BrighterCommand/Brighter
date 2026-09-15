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
/// This runs without a broker, which is the point: <c>rocketmq-ci</c> is commented out in
/// <c>ci.yml</c>, so the generated RocketMQ suite never executes. Nothing else in this repository
/// would notice the guard being removed.
/// </para>
/// </remarks>
[Trait("Category", "RocketMQ")]
public class RocketMqEmptyHeaderPropertyTests
{
    private static Org.Apache.Rocketmq.Message.Builder ABuilder() =>
        new Org.Apache.Rocketmq.Message.Builder()
            .SetTopic("conformance-probe")
            .SetBody([1]);

    private static MessageHeader AHeader() => new(
        messageId: Guid.NewGuid().ToString(),
        topic: new RoutingKey("conformance-probe"),
        messageType: MessageType.MT_EVENT);

    [Fact]
    public void When_baggage_is_empty_should_not_write_the_property()
    {
        // Arrange - a header with no baggage set, which is the common case
        var header = AHeader();
        Assert.Equal(string.Empty, header.Baggage.ToString());

        // Act - would throw if the empty value were handed to AddProperty
        var exception = Record.Exception(
            () => RocketMqMessageProducer.AddHeaderProperties(ABuilder(), header.MessageId, header));

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
        var builder = ABuilder();
        RocketMqMessageProducer.AddHeaderProperties(builder, header.MessageId, header);

        // Assert
        Assert.Contains(HeaderNames.Baggage, builder.Build().Properties.Keys);
    }

    [Fact]
    public void When_the_broker_rejects_an_empty_property_value_should_be_the_reason_for_the_guard()
    {
        // Arrange / Act / Assert - characterises the SDK behaviour the guards exist for, so that a
        // future SDK version quietly accepting empty values shows up here rather than as an
        // unexplained pile of now-pointless conditionals.
        Assert.Throws<ArgumentException>(() => ABuilder().AddProperty("a-key", string.Empty));
    }
}
