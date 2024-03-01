using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

public class DynamoDbOutboxDeleteMessageTests : DynamoDBOutboxBaseTest 
{
    
    [Fact]
    public void When_deleting_a_message_in_the_outbox()
    {
        // arrange
        var message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
        var dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName));
        dynamoDbOutbox.Add(message);

        // act
        dynamoDbOutbox.Delete(new Guid[] {message.Id});

        // assert
        var foundMessage = dynamoDbOutbox.Get(message.Id);
        foundMessage.Header.MessageType.Should().Be(MessageType.MT_NONE);
    }
    
    [Fact]
    public async Task When_deleting_a_message_in_the_outbox_async()
    {
        // arrange
        var message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
        var dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName));
        await dynamoDbOutbox.AddAsync(message);

        // act
        await dynamoDbOutbox.DeleteAsync(new Guid[] {message.Id});

        // assert
        var foundMessage = await dynamoDbOutbox.GetAsync(message.Id);
        foundMessage.Header.MessageType.Should().Be(MessageType.MT_NONE);
    }
}
