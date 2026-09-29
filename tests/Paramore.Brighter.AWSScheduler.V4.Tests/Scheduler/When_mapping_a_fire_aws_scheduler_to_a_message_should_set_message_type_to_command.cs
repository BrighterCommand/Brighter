using Paramore.Brighter.MessageScheduler.AWS.V4;

namespace Paramore.Brighter.AWSScheduler.V4.Tests.Scheduler;

public class AwsSchedulerFiredMapperTests
{
    [Test]
    public async Task When_mapping_a_fire_aws_scheduler_to_a_message_should_set_message_type_to_command()
    {
        // Arrange
        // FireAwsScheduler is a Command, so its wire message must be MT_COMMAND;
        // otherwise the scheduler consumer logs a type mismatch.
        var mapper = new AwsSchedulerFiredMapper();
        var request = new FireAwsScheduler();
        var publication = new Publication { Topic = new RoutingKey("scheduler-topic") };

        // Act
        var message = mapper.MapToMessage(request, publication);

        // Assert
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
    }
}
