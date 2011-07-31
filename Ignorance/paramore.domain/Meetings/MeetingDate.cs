using System;

namespace Paramore.Domain.Meetings
{
    public class MeetingDate
    {
        private readonly DateTime on;

        public MeetingDate(DateTime on)
        {
            this.on = on;
        }

        public static implicit operator DateTime(MeetingDate meetingDate)
        {
            return meetingDate.on;
        }

        public bool Equals(MeetingDate other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.@on.Equals(@on);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (MeetingDate)) return false;
            return Equals((MeetingDate) obj);
        }

        public override int GetHashCode()
        {
            return @on.GetHashCode();
        }

        public static bool operator ==(MeetingDate left, MeetingDate right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(MeetingDate left, MeetingDate right)
        {
            return !Equals(left, right);
        }
    }
}