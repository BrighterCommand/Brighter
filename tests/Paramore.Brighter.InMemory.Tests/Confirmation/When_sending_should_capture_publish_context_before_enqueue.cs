#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Diagnostics;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.InMemory.Tests.Confirmation;

public class ContextCaptureBeforeEnqueueTests
{
    [Test]
    public async Task When_sending_should_capture_publish_context_before_enqueue()
    {
        // Arrange — establish an active publish span before Send
        using var activitySource = new ActivitySource("brighter.test.context.capture");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var publishActivity = activitySource.StartActivity("publish");
        await Assert.That(publishActivity).IsNotNull(); // sampling must be active for the test to be meaningful

        var capturedContext = publishActivity.Context; // what we expect to arrive in the confirmation

        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All)
        {
            UseAsyncPublishConfirmation = true
        };

        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("test.context"), MessageType.MT_DOCUMENT),
            new MessageBody("body"));

        var confirmed = new TaskCompletionSource<PublishConfirmationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.OnMessagePublished += r => confirmed.TrySetResult(r);

        // Act — send while publishActivity is Activity.Current; the producer must capture the
        // context synchronously inside Send, before the work-item crosses to the pump goroutine
        producer.Send(message);

        // Wait for the async pump to drain and raise the confirmation (max 5 s)
        var result = await confirmed.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        // Assert — the publish span context captured at send time flows through to the confirmation
        await Assert.That(result.PublishSpanContext).IsNotNull();
        await Assert.That(result.PublishSpanContext.Value.TraceId).IsEqualTo(capturedContext.TraceId);
        await Assert.That(result.PublishSpanContext.Value.SpanId).IsEqualTo(capturedContext.SpanId);
    }
}