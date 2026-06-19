using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.Azure.Tests.TestDoubles;

// Captures messages handed to ScheduleMessageAsync so tests can assert on the
// envelope the AzureServiceBusScheduler builds, without a live Service Bus.
public class FakeServiceBusSender : ServiceBusSender
{
    private long _sequence;

    public List<ServiceBusMessage> ScheduledMessages { get; } = [];

    public override Task<long> ScheduleMessageAsync(
        ServiceBusMessage message,
        DateTimeOffset scheduledEnqueueTime,
        CancellationToken cancellationToken = default)
    {
        ScheduledMessages.Add(message);
        return Task.FromResult(++_sequence);
    }
}
