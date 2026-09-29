using Azure.Messaging.ServiceBus;
using Paramore.Brighter.Azure.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Azure;

namespace Paramore.Brighter.Azure.Tests.Scheduler;

// The scheduler wraps every scheduled message/request in a FireAzureScheduler envelope.
// FireAzureScheduler is a Command, so the envelope's wire MessageType must be MT_COMMAND on
// all four overloads (sync/async x message/request); otherwise the scheduler-topic consumer
// dispatches it as an event and MessagePump.ValidateMessageType logs a spurious mismatch.
public class AzureServiceBusSchedulerMessageTypeTests
{
    private const string MessageTypeKey = "MessageType";

    private readonly FakeServiceBusSender _sender = new();
    private readonly AzureServiceBusScheduler _scheduler;
    private readonly DateTimeOffset _fireAt = DateTimeOffset.UtcNow.AddDays(1);

    public AzureServiceBusSchedulerMessageTypeTests()
        => _scheduler = new AzureServiceBusScheduler(_sender, new RoutingKey("scheduler-topic"), TimeProvider.System);

    [Test]
    public async Task When_scheduling_a_message_async_should_set_message_type_to_command()
    {
        // Arrange
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("a-topic"), MessageType.MT_EVENT),
            new MessageBody("body"));

        // Act
        await _scheduler.ScheduleAsync(message, _fireAt);

        // Assert
        await Assert.That(ScheduledMessageType()).IsEqualTo(MessageType.MT_COMMAND.ToString());
    }

    [Test]
    public async Task When_scheduling_a_request_async_should_set_message_type_to_command()
    {
        // Arrange
        var request = new SuperAwesomeCommand("do the thing");

        // Act
        await _scheduler.ScheduleAsync(request, RequestSchedulerType.Send, _fireAt);

        // Assert
        await Assert.That(ScheduledMessageType()).IsEqualTo(MessageType.MT_COMMAND.ToString());
    }

    [Test]
    public async Task When_scheduling_a_message_sync_should_set_message_type_to_command()
    {
        // Arrange
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("a-topic"), MessageType.MT_EVENT),
            new MessageBody("body"));

        // Act
        _scheduler.Schedule(message, _fireAt);

        // Assert
        await Assert.That(ScheduledMessageType()).IsEqualTo(MessageType.MT_COMMAND.ToString());
    }

    [Test]
    public async Task When_scheduling_a_request_sync_should_set_message_type_to_command()
    {
        // Arrange
        var request = new SuperAwesomeCommand("do the thing");

        // Act
        _scheduler.Schedule(request, RequestSchedulerType.Send, _fireAt);

        // Assert
        await Assert.That(ScheduledMessageType()).IsEqualTo(MessageType.MT_COMMAND.ToString());
    }

    private object ScheduledMessageType()
    {
        ServiceBusMessage scheduled = _sender.ScheduledMessages.Single();
        return scheduled.ApplicationProperties[MessageTypeKey];
    }
}
