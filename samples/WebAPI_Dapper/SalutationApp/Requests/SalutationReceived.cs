using System;
using Paramore.Brighter;

namespace SalutationApp.Requests;

public class SalutationReceived : Event
{
    public SalutationReceived(DateTimeOffset receivedAt) : base(Guid.NewGuid())
    {
        ReceivedAt = receivedAt;
    }

    public DateTimeOffset ReceivedAt { get; }
}
