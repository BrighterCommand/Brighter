using System;
using Paramore.Brighter;

namespace SalutationApp.Requests
{
    public class SalutationReceived(DateTimeOffset receivedAt) : Event(Id.Random())
    {
        public DateTimeOffset ReceivedAt { get; } = receivedAt;
    }
}
