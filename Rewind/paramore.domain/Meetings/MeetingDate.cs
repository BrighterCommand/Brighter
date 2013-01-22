using System;
using Paramore.Domain.Common;

namespace Paramore.Domain.Meetings
{
    public class MeetingDate : IEquatable<MeetingDate>, IAmAValueType<DateTime>
    {
        private readonly DateTime on;

        public MeetingDate(DateTime on)
        {
            this.on = on;
        }

        public DateTime Value
        {
            get { return on; }
        }

        public static implicit operator DateTime(MeetingDate rhs)
        {
            return rhs.on;
        }

        public bool Equals(MeetingDate rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return rhs.@on.Equals(@on);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (MeetingDate)) return false;
            return Equals((MeetingDate) rhs);
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

        public override string ToString()
        {
            return string.Format("{0}", @on);
        }

    }
}