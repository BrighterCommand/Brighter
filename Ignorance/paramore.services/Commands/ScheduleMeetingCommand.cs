using System;

namespace Paramore.Services.Commands
{
    [Serializable]
    public class ScheduleMeetingCommand : Command
    {
        public DateTime On { get; private set; }
        public Guid LocationId { get; private set; }
        public Guid SpeakerId { get; private set; }
        public int Capacity { get; private set; }
        public Guid MeetingId { get; private set; }

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
