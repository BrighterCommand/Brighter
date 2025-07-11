using System;
using Paramore.Brighter;

namespace SalutationApp.Requests
{
    public class SalutationReceived(DateTimeOffset receivedAt) : Event(Guid.NewGuid().ToString())
    {
        public DateTimeOffset ReceivedAt { get; } = receivedAt;
    }
}
