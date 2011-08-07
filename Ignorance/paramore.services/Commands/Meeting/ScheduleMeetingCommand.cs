using System;
using Paramore.Services.CommandProcessors;
using Paramore.Services.Common;

namespace Paramore.Services.Commands.Meeting
{
    [Serializable]
    public class ScheduleMeetingCommand : Command, IRequest
    {
        public DateTime On { get; set; }
        public Guid VenueId { get; set; }
        public Guid SpeakerId { get; set; }
        public int Capacity { get; set; }
        public Guid MeetingId { get; set; }

        public ScheduleMeetingCommand(Guid id) : base(id) { }

        public ScheduleMeetingCommand(Guid id, DateTime on, Guid location, Guid speaker, int capacity) : base(id)
        {
            On = on;
            VenueId = location;
            SpeakerId = speaker;
            Capacity = capacity;
            MeetingId = id;
        }

        public ScheduleMeetingCommand() : base(Guid.NewGuid()) {}
    }
}
