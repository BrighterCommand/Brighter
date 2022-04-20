using System;
using Paramore.Brighter;

namespace SalutationPorts.Requests
{
    public class SalutationReceived : Event
    {
        public DateTimeOffset ReceivedAt { get; }

        public SalutationReceived(DateTimeOffset receivedAt) : base(Guid.NewGuid())
        {
            ReceivedAt = receivedAt;
        }
    }
}
