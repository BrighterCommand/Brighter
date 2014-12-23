using System;
using System.Text.RegularExpressions;

namespace Tasks.Model
{
    public class EmailAddress : IEquatable<EmailAddress>
    {
        private readonly string emailAddress;

        private const string VALID_EMAIL_REGEX = @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z";

        public string Value { get { return emailAddress; } }

        public EmailAddress(string emailAddress)
        {
            bool isEmail = Regex.IsMatch(emailAddress, VALID_EMAIL_REGEX);
            if (!isEmail)
                throw new ArgumentException("Email address was not valid", "emailAddress");

            this.emailAddress = emailAddress;
        }

        public override string ToString()
        {
            return emailAddress;
        }

        public static implicit operator string(EmailAddress rhs)
        {
            return rhs.Value;
        }

        public bool Equals(EmailAddress other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(emailAddress, other.emailAddress);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EmailAddress) obj);
        }

        public override int GetHashCode()
        {
            return emailAddress.GetHashCode();
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