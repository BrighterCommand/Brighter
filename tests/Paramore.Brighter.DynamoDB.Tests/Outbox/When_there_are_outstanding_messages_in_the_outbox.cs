using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

[Trait("Category", "DynamoDB")]
public class DynamoDbOutboxOutstandingMessageTests : DynamoDBOutboxBaseTest
{
    private readonly Message _message;
    private readonly DynamoDbOutbox _dynamoDbOutbox;

    public DynamoDbOutboxOutstandingMessageTests()
    {
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_DOCUMENT), 
            new MessageBody("message body")
        );
        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName));
    }

    [Fact]
    public async Task When_there_are_outstanding_messages_in_the_outbox_async()
    {
        var context = new RequestContext();
        await _dynamoDbOutbox.AddAsync(_message, context);

        await Task.Delay(1000);

        var args = new Dictionary<string, object> {{"Topic", "test_topic"}};

        var messages = await _dynamoDbOutbox.OutstandingMessagesAsync(0, context, 100, 1, args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Should().Be(_message.Body);
    }

    [Fact]
    public async Task When_there_are_outstanding_messages_in_the_outbox()
    {
        var context = new RequestContext();
        _dynamoDbOutbox.Add(_message, context);

        await Task.Delay(1000);

        var args = new Dictionary<string, object> {{"Topic", "test_topic"}};

        var messages =_dynamoDbOutbox.OutstandingMessages(0, context, 100, 1, args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Should().Be(_message.Body);
    }
}
