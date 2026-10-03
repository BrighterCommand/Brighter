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
using System.Diagnostics;
using Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull.Reactor;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

// Real-GCP only: the local Pub/Sub emulator does not reproduce this race (see #4321), so this
// test is only meaningful against real Pub/Sub, i.e. in the gcp-ci job or locally with
// GOOGLE_CLOUD_PROJECT + Application Default Credentials set and PUBSUB_EMULATOR_HOST unset.
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class RequeueZeroDelayShortPollWindowTests : IDisposable
{
    private readonly IAmAMessageGatewayReactorProvider _messageGatewayProvider;
    private readonly IAmAMessageBuilder _messageBuilder;
    private readonly IAmAMessageAssertion _messageAssertion;

    private readonly List<Message> _sentMessages = [];

    private Paramore.Brighter.MessagingGateway.GcpPubSub.GcpPubSubSubscription? _subscription;
    private Paramore.Brighter.MessagingGateway.GcpPubSub.GcpPublication? _publication;

    private IAmAMessageProducerSync? _producer;
    private IAmAChannelSync? _channel;

    public RequeueZeroDelayShortPollWindowTests()
    {
        _messageGatewayProvider = new GcpPullMessageGatewayProvider();
        _messageBuilder = new DefaultMessageBuilder();
        _messageAssertion = new DefaultMessageAssertion();
    }

    public void Dispose()
    {
        _messageGatewayProvider.CleanUp(_producer, _channel, _sentMessages);
    }

    [Fact]
    public void When_requeuing_a_message_with_zero_delay_and_polling_with_a_short_window_should_still_redeliver_promptly()
    {
        // Arrange
        _publication = _messageGatewayProvider.CreatePublication(_messageGatewayProvider.GetOrCreateRoutingKey());
        _subscription = _messageGatewayProvider.CreateSubscription(_publication.Topic!,
            _messageGatewayProvider.GetOrCreateChannelName(),
            OnMissingChannel.Create);

        _producer = _messageGatewayProvider.CreateProducer(_publication);
        _channel = _messageGatewayProvider.CreateChannel(_subscription);

        var message = _messageBuilder.SetTopic(_publication.Topic!).Build();
        _sentMessages.Add(message);

        _producer.Send(message);

        // Act — receive the message and requeue it with an explicit TimeSpan.Zero
        var received = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

        var requeued = _channel.Requeue(received, TimeSpan.Zero);
        Assert.True(requeued);

        // The window opens when Requeue RETURNS. The call's own duration is a round trip to the
        // broker to issue the instruction — the cost of asking, not a delay applied to the
        // message — so charging it to the budget would measure network latency rather than the
        // gateway's treatment of TimeSpan.Zero.
        var stopwatch = Stopwatch.StartNew();

        // Assert — poll every 500 ms (the same short, client-cancelled-Pull-prone window the
        // production pump uses by default), giving up after 30 s. The modack itself is proven to
        // take effect within ~2s regardless of poll window (bugfix.md, runs 2/3 against real
        // Pub/Sub) — redelivery must not cost a full extra ack-deadline cycle on top of that.
        var redelivered = new Message();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            redelivered = _channel.Receive(TimeSpan.FromMilliseconds(500));
            if (redelivered.Header.MessageType != MessageType.MT_NONE)
            {
                break;
            }
        }

        Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Expected redelivery within 5 s of Requeue(M, TimeSpan.Zero) returning; elapsed: {stopwatch.Elapsed}");
        _messageAssertion.Assert(message, redelivered);
    }
}
