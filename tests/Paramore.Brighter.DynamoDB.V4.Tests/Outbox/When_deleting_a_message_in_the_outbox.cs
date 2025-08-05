using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Outbox.DynamoDB.V4;
using Xunit;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

public class DynamoDbOutboxDeleteMessageTests : DynamoDBOutboxBaseTest 
{
    private readonly FakeTimeProvider _fakeTimeProvider = new FakeTimeProvider();

    [Fact]
    public void When_deleting_a_message_in_the_outbox()
    {
        // arrange
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("test_topic"), MessageType.MT_DOCUMENT), 
            new MessageBody("message body")
            );
        
        var context = new RequestContext();
        var dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName), _fakeTimeProvider);
        dynamoDbOutbox.Add(message, context);

        // act
        dynamoDbOutbox.Delete([message.Id], context);

        // assert
        var foundMessage = dynamoDbOutbox.Get(message.Id, context);
        Assert.Equal(MessageType.MT_NONE, foundMessage.Header.MessageType);
    }
    
    [Fact]
    public async Task When_deleting_a_message_in_the_outbox_async()
    {
        // arrange
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test_topic"), 
                MessageType.MT_DOCUMENT), new MessageBody("message body"));
        var context = new RequestContext();
        var dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName), _fakeTimeProvider);
        await dynamoDbOutbox.AddAsync(message, context);

        // act
        await dynamoDbOutbox.DeleteAsync([message.Id], context);

        // assert
        var foundMessage = await dynamoDbOutbox.GetAsync(message.Id, context);
        Assert.Equal(MessageType.MT_NONE, foundMessage.Header.MessageType);
    }
}
