using Paramore.Brighter.MessageScheduler.Azure;

namespace Paramore.Brighter.Azure.Tests.Scheduler;

public class AzureSchedulerFiredMapperTests
{
    [Test]
    public void When_mapping_a_fire_azure_scheduler_to_a_message_should_set_message_type_to_command()
    {
        // Arrange
        // FireAzureScheduler is a Command, so its wire message must be MT_COMMAND;
        // otherwise the scheduler-topic consumer logs a type mismatch.
        var mapper = new AzureSchedulerFiredMapper();
        var request = new FireAzureScheduler();
        var publication = new Publication { Topic = new RoutingKey("scheduler-topic") };

        // Act
        var message = mapper.MapToMessage(request, publication);

        // Assert
        Assert.That(message.Header.MessageType, Is.EqualTo(MessageType.MT_COMMAND));
    }
}
