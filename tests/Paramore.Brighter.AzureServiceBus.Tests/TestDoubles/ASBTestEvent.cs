using System;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles
{
    public class ASBTestEvent : Event
    {
        public ASBTestEvent() : base(Guid.NewGuid().ToString())
        {
        }

        public string EventName { get; set; }
        public int EventNumber { get; set; }
    }
}
