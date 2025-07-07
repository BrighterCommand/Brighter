using System;
using Paramore.Brighter;

namespace SalutationApp.Requests
{
    public class SalutationReceived : Event
    {
        public DateTimeOffset ReceivedAt { get; }

        public SalutationReceived(DateTimeOffset receivedAt) : base(Guid.NewGuid().ToString())
        {
            ReceivedAt = receivedAt;
        }
    }
}
