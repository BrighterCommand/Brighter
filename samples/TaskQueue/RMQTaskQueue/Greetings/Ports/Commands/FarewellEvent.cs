using System;
using MessagePack;
using Paramore.Brighter;

namespace Greetings.Ports.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class FarewellEvent(string farewell) : Event(Id.Random)
    {
        public string Farewell { get; set; } = farewell;
    }
}
