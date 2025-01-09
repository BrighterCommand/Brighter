using System;
using MessagePack;
using Paramore.Brighter;

namespace Greetings.Ports.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class FarewellEvent(string farewell) : Event(Guid.NewGuid().ToString())
    {
        public string Farewell { get; set; } = farewell;
    }
}
