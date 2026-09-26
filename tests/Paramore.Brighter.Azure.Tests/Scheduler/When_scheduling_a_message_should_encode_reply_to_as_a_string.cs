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

using System.Text.Json;
using Paramore.Brighter.Azure.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageScheduler.Azure;

namespace Paramore.Brighter.Azure.Tests.Scheduler;

public class AzureScheduledMessageReplyToTests
{
    [TestCase(false, "")]
    [TestCase(false, "reply-queue")]
    [TestCase(true, "")]
    [TestCase(true, "reply-queue")]
    public async Task When_scheduling_a_message_should_encode_reply_to_as_a_string(bool isAsync, string replyTo)
    {
        //Arrange
        var sender = new FakeServiceBusSender();
        var scheduler = new AzureServiceBusScheduler(sender, new RoutingKey("scheduler-topic"), TimeProvider.System);
        var message = new Message(new MessageHeader(Id.Random(), new RoutingKey("events"), MessageType.MT_EVENT,
            replyTo: new RoutingKey(replyTo)), new MessageBody("test"));

        //Act
        if (isAsync)
            await scheduler.ScheduleAsync(message, TimeSpan.FromMinutes(1));
        else
            scheduler.Schedule(message, TimeSpan.FromMinutes(1));

        //Assert
        var scheduled = sender.ScheduledMessages.Single();
        Assert.That(scheduled.ApplicationProperties["ReplyTo"], Is.TypeOf<string>());
        Assert.That(scheduled.ApplicationProperties["ReplyTo"], Is.EqualTo(string.Empty));
        var envelope = JsonSerializer.Deserialize<FireAzureScheduler>(scheduled.Body.ToString(), JsonSerialisationOptions.Options)!;
        Assert.That(envelope.Message!.Header.ReplyTo!.Value, Is.EqualTo(replyTo));
    }
}
