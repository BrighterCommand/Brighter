using System;
using System.Text.RegularExpressions;
using Paramore.Domain.Common;

namespace Paramore.Domain.Venues
{
    public class Contact : IAmAValueType<string>, IEquatable<Contact>
    {
        private readonly Name name;
        private readonly EmailAddress emailAddress;
        private readonly PhoneNumber phoneNumber;

        public Contact(Name name, EmailAddress emailAddress, PhoneNumber phoneNumber)
        {
            this.name = name;
            this.emailAddress = emailAddress;
            this.phoneNumber = phoneNumber;
        }

        public Contact()
        {
            name = new Name();
            emailAddress = new EmailAddress();
            phoneNumber = new PhoneNumber();
        }

        public string Value
        {
            get { return ToString();  }
    }

        public static implicit operator string(Contact rhs)
        {
            return rhs.ToString();
        }

        public static Contact Parse(string venueContact)
        {
            var rx = new Regex("Name : (.*), EmailAddress : (.*): , PhoneNumber : (.*)");
            var match = rx.Match(venueContact);
            var contactName = new Name(match.Groups[0].ToString());
            var emailAddress = new EmailAddress(match.Groups[1].ToString());
            var phoneNumber = new PhoneNumber(match.Groups[2].ToString());
            return new Contact(contactName, emailAddress, phoneNumber);
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, EmailAddress: {1}, PhoneNumber: {2}", name, emailAddress, phoneNumber);
        }

        public bool Equals(Contact rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.name, name) && Equals(rhs.emailAddress, emailAddress) && Equals(rhs.phoneNumber, phoneNumber);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (Contact)) return false;
            return Equals((Contact) rhs);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (name != null ? name.GetHashCode() : 0);
                result = (result*397) ^ (emailAddress != null ? emailAddress.GetHashCode() : 0);
                result = (result*397) ^ (phoneNumber != null ? phoneNumber.GetHashCode() : 0);
                return result;
            }
        }

        public static bool operator ==(Contact left, Contact right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Contact left, Contact right)
        {
            return !Equals(left, right);
        }
    }
}