using System;
using MessagePack;
using Paramore.Brighter;

namespace Greetings.Ports.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class FarewellEvent : Event
    {
        public FarewellEvent() : base(Guid.NewGuid())
        {
        }

        public FarewellEvent(string farewell) : base(Guid.NewGuid())
        {
            Farewell = farewell;
        }

        public string Farewell { get; set; }
    }
}
