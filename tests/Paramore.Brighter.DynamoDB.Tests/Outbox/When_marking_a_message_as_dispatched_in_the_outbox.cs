using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using FluentAssertions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

[Trait("Category", "DynamoDB")]
public class DynamoDbOutboxMessageDispatchTests : DynamoDBOutboxBaseTest
{
    private readonly Message _message;
    private readonly DynamoDbOutbox _dynamoDbOutbox;

    public DynamoDbOutboxMessageDispatchTests()
    {
        _message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(Credentials, RegionEndpoint.EUWest1, OutboxTableName));
    }

    [Fact]
    public async Task When_Marking_A_Message_As_Dispatched_In_The_Outbox_Async()
    {
        await _dynamoDbOutbox.AddAsync(_message);
        await _dynamoDbOutbox.MarkDispatchedAsync(_message.Id, DateTime.UtcNow);
        
        var args = new Dictionary<string, object>(); 
        args.Add("Topic", "test_topic");

        var messages = _dynamoDbOutbox.DispatchedMessages(0, 100, 1, args:args);
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Should().Be(_message.Body);
    }

    [Fact]
    public void When_Marking_A_Message_As_Dispatched_In_The_Outbox()
    {
        _dynamoDbOutbox.Add(_message);  
        _dynamoDbOutbox.MarkDispatched(_message.Id, DateTime.UtcNow);

        var args = new Dictionary<string, object>(); 
        args.Add("Topic", "test_topic");

        var messages = _dynamoDbOutbox.DispatchedMessages(0, 100, 1, args:args);
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Should().Be(_message.Body);
    }
}
