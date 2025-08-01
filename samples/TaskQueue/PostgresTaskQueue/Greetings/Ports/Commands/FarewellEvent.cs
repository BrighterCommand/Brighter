using System;
using MessagePack;
using Paramore.Brighter;

namespace Greetings.Ports.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class FarewellEvent : Event
    {
        public FarewellEvent() : base(Id.Random())
        {
        }

        public FarewellEvent(string farewell) : base(Id.Random())
        {
            Farewell = farewell;
        }

        public string Farewell { get; set; }
    }
}
