#region Licence

/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbOutboxWritingRawMessageTests : DynamoDBOutboxBaseTest
    {
        [Fact]
        public void When_writing_a_raw_message_to_the_dynamo_db_outbox()
        {
           //arrange
           //Uses Kafka header of 5 leading bytes, with schema id as an example
            var command = new MyCommand { Value = "Test", WasCancelled = false, TaskCompleted = false };
            var schemaId = 1234;
            var body = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
            var magicByte = new byte[] { 0 };
            var schemaIdAsBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(schemaId));
            var payload = magicByte.Concat(schemaIdAsBytes).ToArray();
            var serdesBody = payload.Concat(Encoding.ASCII.GetBytes(body)).ToArray();

            var messageHeader = new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test_topic"),
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTime.UtcNow.AddDays(-1),
                handledCount: 5,
                delayed: TimeSpan.FromMilliseconds(5),
                correlationId: Guid.NewGuid().ToString(),
                replyTo: new RoutingKey("ReplyAddress"),
                contentType: "text/plain");

            var fakeTimeProvider = new FakeTimeProvider();
            var dynamoDbOutbox = new DynamoDbOutbox(Client,
                new DynamoDbConfiguration(OutboxTableName), fakeTimeProvider);

            var messageEarliest = new Message(
                messageHeader,
                new MessageBody(serdesBody, MediaTypeNames.Application.Octet, CharacterEncoding.Raw));
            
            var context = new RequestContext();

            //act
            dynamoDbOutbox.Add(messageEarliest, context);

            var storedMessage = dynamoDbOutbox.Get(messageEarliest.Id, context);
            var retrievedSchemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(storedMessage.Body.Bytes.Skip(1).Take(4).ToArray()));

            //assert
            Assert.Equal(schemaId, retrievedSchemaId);
            storedMessage.Body.Bytes.Should().Equal(messageEarliest.Body.Bytes);
            Assert.Equal(messageEarliest.Body.Value, storedMessage.Body.Value);
        }
    }
}
