using Paramore.Brighter.MessageScheduler.AWS;

namespace Paramore.Brighter.AWSScheduler.Tests.Scheduler;

public class AwsSchedulerFiredMapperTests
{
    [Fact]
    public void When_mapping_a_fire_aws_scheduler_to_a_message_should_set_message_type_to_command()
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
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
    }
}
