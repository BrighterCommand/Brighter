using System;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles
{
    public class ASBTestEvent : Event
    {
        public ASBTestEvent() : base(Guid.NewGuid())
        {
        }

        public string EventName { get; set; }
        public int EventNumber { get; set; }
    }
}
