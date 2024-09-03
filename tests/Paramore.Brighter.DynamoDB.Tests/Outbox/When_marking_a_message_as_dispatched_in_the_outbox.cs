using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

[Trait("Category", "DynamoDB")]
public class DynamoDbOutboxMessageDispatchTests : DynamoDBOutboxBaseTest
{
    private readonly Message _message;
    private readonly DynamoDbOutbox _dynamoDbOutbox;
    private readonly FakeTimeProvider _fakeTimeProvider;

    public DynamoDbOutboxMessageDispatchTests()
    {
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_DOCUMENT), 
            new MessageBody("message body")
        );
        _fakeTimeProvider = new FakeTimeProvider();
        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName), _fakeTimeProvider);
    }

    [Fact]
    public async Task When_Marking_A_Message_As_Dispatched_In_The_Outbox_Async()
    {
        var context = new RequestContext();
        await _dynamoDbOutbox.AddAsync(_message, context);
        await _dynamoDbOutbox.MarkDispatchedAsync(_message.Id, context, _fakeTimeProvider.GetUtcNow().DateTime);
        
        var args = new Dictionary<string, object>(); 
        args.Add("Topic", "test_topic");

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var messages = _dynamoDbOutbox.DispatchedMessages(TimeSpan.Zero, context,100, 1, args:args);
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Value.Should().Be(_message.Body.Value);
    }

    [Fact]
    public void When_Marking_A_Message_As_Dispatched_In_The_Outbox()
    {
        var context = new RequestContext();
        _dynamoDbOutbox.Add(_message, context);  
        _dynamoDbOutbox.MarkDispatched(_message.Id, context, _fakeTimeProvider.GetUtcNow().DateTime);

        var args = new Dictionary<string, object>(); 
        args.Add("Topic", "test_topic");

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var messages = _dynamoDbOutbox.DispatchedMessages(TimeSpan.Zero, context, 100, 1, args:args);
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Value.Should().Be(_message.Body.Value);
    }
}
