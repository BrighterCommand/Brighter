using System;
using Paramore.Services.CommandProcessors;

namespace Paramore.Services.Commands.Meeting
{
    [Serializable]
    public class ScheduleMeetingCommand : Command
    {
        public DateTime On { get; set; }
        public Guid LocationId { get; set; }
        public Guid SpeakerId { get; set; }
        public int Capacity { get; set; }
        public Guid MeetingId { get; set; }

        public ScheduleMeetingCommand(Guid id) : base(id) { }

        public ScheduleMeetingCommand(Guid id, DateTime on, Guid location, Guid speaker, int capacity) : base(id)
        {
            On = on;
            LocationId = location;
            SpeakerId = speaker;
            Capacity = capacity;
            MeetingId = id;
        }

    }
}
