#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

[Property("Category", "AWS")]
public class AwsCloudEventSourceWriterTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_publishing_an_aws_message_should_write_the_canonical_source_header(bool useSns)
    {
        //Arrange
        var connection = GatewayFactory.CreateFactory();
        var reaper = new AwsTestResourceReaper(connection);
        var queueName = reaper.TrackQueue($"Source-Writer-{Guid.NewGuid():N}");
        var topicName = reaper.TrackTopic($"Source-Writer-{Guid.NewGuid():N}");
        var factory = new ChannelFactory(connection);
        using var sqs = new AWSClientFactory(connection).CreateSqsClient();
        using var sns = new AWSClientFactory(connection).CreateSnsClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        IAmAChannelAsync? channel = null;
        try
        {
            channel = await factory.CreateAsyncChannelAsync(new SqsSubscription(
                new SubscriptionName(queueName),
                new ChannelName(queueName),
                ChannelType.PubSub,
                new RoutingKey(topicName),
                requestType: typeof(Command),
                messagePumpType: MessagePumpType.Proactor,
                queueAttributes: new SqsAttributes(rawMessageDelivery: true,
                    tags: new Dictionary<string, string> { ["Environment"] = "Test" })),
                timeout.Token);

            var queueUrl = (await sqs.GetQueueUrlAsync(queueName, timeout.Token)).QueueUrl;
            var topicArn = (await sns.CreateTopicAsync(topicName, timeout.Token)).TopicArn;

            var source = new Uri("https://orders.example.test/events");
            var message = new Message(
                new MessageHeader(Id.Random(), new RoutingKey(topicName), MessageType.MT_EVENT, source: source),
                new MessageBody("source compatibility"));

            //Act
            var messageId = useSns
                ? await new SnsMessagePublisher(topicArn, sns).PublishAsync(message)
                : await new SqsMessageSender(queueUrl, sqs).SendAsync(message, TimeSpan.Zero, timeout.Token);
            var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MessageAttributeNames = ["All"],
                WaitTimeSeconds = 10,
                MaxNumberOfMessages = 1
            }, timeout.Token);

            //Assert
            await Assert.That(string.IsNullOrEmpty(messageId)).IsFalse();
            await Assert.That(response.Messages).IsNotNull();
            await Assert.That(response.Messages).HasSingleItem();
            var received = response.Messages.Single();
            await Assert.That(received.Body).IsEqualTo(message.Body.Value);
            using var headers = JsonDocument.Parse(received.MessageAttributes["cloudeventheaders"].StringValue);
            await Assert.That(headers.RootElement.TryGetProperty("source", out var actualSource)).IsTrue();
            await Assert.That(actualSource.GetString()).IsEqualTo(source.ToString());
            await Assert.That(headers.RootElement.TryGetProperty("souce", out _)).IsFalse();
        }
        finally
        {
            try
            {
                if (channel != null)
                    await channel.DisposeAsync();
            }
            finally
            {
                await reaper.ReapAsync();
            }
        }
    }
}
