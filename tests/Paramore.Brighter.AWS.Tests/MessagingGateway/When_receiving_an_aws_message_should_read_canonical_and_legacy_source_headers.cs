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
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

[Trait("Category", "AWS")]
public class AwsCloudEventSourceReaderTests
{
    private const string CANONICAL = "https://canonical.example.test/events";
    private const string LEGACY = "https://legacy.example.test/events";
    private const string NESTED = "https://nested.example.test/events";
    private const string INVALID = "http://[invalid";

    public static IEnumerable<object?[]> SourceCases()
    {
        // raw delivery, standalone source/souce, nested source/souce, expected source
        object?[][] cases =
        [
            [true, null, null, CANONICAL, null, CANONICAL],
            [true, null, null, null, LEGACY, LEGACY],
            [true, null, null, CANONICAL, LEGACY, CANONICAL],
            [true, null, null, INVALID, LEGACY, LEGACY],
            [true, null, null, null, null, MessageHeader.DefaultSource],
            [true, null, null, INVALID, INVALID, MessageHeader.DefaultSource],
            [true, null, null, "/orders/events", null, "/orders/events"],
            [true, CANONICAL, LEGACY, null, null, MessageHeader.DefaultSource],
            [false, CANONICAL, null, null, null, CANONICAL],
            [false, null, LEGACY, null, null, LEGACY],
            [false, CANONICAL, LEGACY, null, null, CANONICAL],
            [false, null, null, CANONICAL, null, CANONICAL],
            [false, null, null, null, LEGACY, LEGACY],
            [false, null, null, CANONICAL, LEGACY, CANONICAL],
            [false, null, null, INVALID, LEGACY, LEGACY],
            [false, null, LEGACY, NESTED, null, LEGACY],
            [false, CANONICAL, null, null, NESTED, CANONICAL],
            [false, INVALID, LEGACY, NESTED, null, LEGACY],
            [false, INVALID, INVALID, NESTED, null, NESTED],
            [false, null, null, null, null, MessageHeader.DefaultSource],
            [false, INVALID, INVALID, INVALID, INVALID, MessageHeader.DefaultSource],
            [false, "/orders/events", null, null, null, "/orders/events"]
        ];

        foreach (var testCase in cases)
        {
            foreach (var useAsync in new[] { false, true })
                yield return testCase.Append((object)useAsync).ToArray();
        }
    }

    [Theory]
    [MemberData(nameof(SourceCases))]
    public async Task When_receiving_an_aws_message_should_read_canonical_and_legacy_source_headers(
        bool rawDelivery, string? standaloneSource, string? standaloneLegacySource,
        string? nestedSource, string? nestedLegacySource, string expectedSource, bool useAsync)
    {
        //Arrange
        var connection = GatewayFactory.CreateFactory();
        var reaper = new AwsTestResourceReaper(connection);
        var queueName = reaper.TrackQueue($"Source-Reader-{Guid.NewGuid():N}");
        using var sqs = new AWSClientFactory(connection).CreateSqsClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var queueUrl = (await sqs.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = queueName,
                Tags = new Dictionary<string, string> { ["Environment"] = "Test" }
            }, timeout.Token)).QueueUrl;
            var id = Id.Random();
            var headers = new Dictionary<string, string>();
            if (nestedSource != null)
                headers["source"] = nestedSource;
            if (nestedLegacySource != null)
                headers["souce"] = nestedLegacySource;

            var attributes = new Dictionary<string, string>
            {
                ["id"] = id.Value,
                ["topic"] = "orders",
                ["cloudeventheaders"] = JsonSerializer.Serialize(headers),
                ["external-note"] = "preserved"
            };
            if (standaloneSource != null)
                attributes["source"] = standaloneSource;
            if (standaloneLegacySource != null)
                attributes["souce"] = standaloneLegacySource;

            const string body = "source compatibility";
            var request = new SendMessageRequest { QueueUrl = queueUrl, MessageBody = body };
            if (rawDelivery)
            {
                request.MessageAttributes = attributes.ToDictionary(pair => pair.Key,
                    pair => new MessageAttributeValue { DataType = "String", StringValue = pair.Value });
            }
            else
            {
                request.MessageBody = JsonSerializer.Serialize(new
                {
                    Type = "Notification",
                    Message = body,
                    MessageAttributes = attributes.ToDictionary(pair => pair.Key,
                        pair => new { Type = "String", Value = pair.Value })
                });
            }

            await sqs.SendMessageAsync(request, timeout.Token);
            await using var consumer = new SqsMessageConsumer(connection, queueUrl,
                makeChannels: OnMissingChannel.Assume, isQueueUrl: true, rawMessageDelivery: rawDelivery);

            //Act
            var received = useAsync
                ? await consumer.ReceiveAsync(TimeSpan.FromSeconds(10), timeout.Token)
                : consumer.Receive(TimeSpan.FromSeconds(10));

            //Assert
            var message = Assert.Single(received);
            Assert.Equal(id, message.Id);
            Assert.Equal(body, message.Body.Value);
            Assert.Equal(new Uri(expectedSource, UriKind.RelativeOrAbsolute), message.Header.Source);
            Assert.DoesNotContain("source", message.Header.Bag.Keys);
            Assert.DoesNotContain("souce", message.Header.Bag.Keys);
            Assert.Equal("preserved", message.Header.Bag["external-note"]);
        }
        finally
        {
            await reaper.ReapAsync();
        }
    }
}
