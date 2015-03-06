using System;

namespace Paramore.Rewind.Core.Domain.Common
{
    public class EmailAddress : IEquatable<EmailAddress>, IAmAValueType<string>
    {
        private readonly string email = string.Empty;

        public EmailAddress(string email)
        {
            this.email = email;
        }

        public EmailAddress()
        {
        }

        public string Value
        {
            get { return email; }
        }

        public bool Equals(EmailAddress lhs)
        {
            if (ReferenceEquals(null, lhs)) return false;
            if (ReferenceEquals(this, lhs)) return true;
            return Equals(lhs.email, email);
        }

        public static implicit operator string(EmailAddress lhs)
        {
            return lhs.email;
        }

        public override string ToString()
        {
            return string.Format("{0}", email);
        }

        public override bool Equals(object lhs)
        {
            if (ReferenceEquals(null, lhs)) return false;
            if (ReferenceEquals(this, lhs)) return true;
            if (lhs.GetType() != typeof (EmailAddress)) return false;
            return Equals((EmailAddress) lhs);
        }

        public override int GetHashCode()
        {
            return (email != null ? email.GetHashCode() : 0);
        }

        public static bool operator ==(EmailAddress left, EmailAddress right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(EmailAddress left, EmailAddress right)
        {
            return !Equals(left, right);
        }
    }
}