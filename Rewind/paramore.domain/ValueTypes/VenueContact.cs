using System;
using System.Text.RegularExpressions;

namespace Paramore.Domain.ValueTypes
{
    public class VenueContact : IAmAValueType<string>, IEquatable<VenueContact>
    {
        private readonly ContactName contactName;
        private readonly EmailAddress emailAddress;
        private readonly PhoneNumber phoneNumber;

        public VenueContact(ContactName contactName, EmailAddress emailAddress, PhoneNumber phoneNumber)
        {
            this.contactName = contactName;
            this.emailAddress = emailAddress;
            this.phoneNumber = phoneNumber;
        }

        public VenueContact()
        {
            contactName = new ContactName();
            emailAddress = new EmailAddress();
            phoneNumber = new PhoneNumber();
        }

        public string Value
        {
            get { return ToString();  }
    }

        public static implicit operator string(VenueContact rhs)
        {
            return rhs.ToString();
        }

        public static VenueContact Parse(string venueContact)
        {
            var rx = new Regex("Name : (.*), EmailAddress : (.*): , PhoneNumber : (.*)");
            var match = rx.Match(venueContact);
            var contactName = new ContactName(match.Groups[0].ToString());
            var emailAddress = new EmailAddress(match.Groups[1].ToString());
            var phoneNumber = new PhoneNumber(match.Groups[2].ToString());
            return new VenueContact(contactName, emailAddress, phoneNumber);
        }

        public override string ToString()
        {
            return string.Format("ContactName: {0}, EmailAddress: {1}, PhoneNumber: {2}", contactName, emailAddress, phoneNumber);
        }

        public bool Equals(VenueContact rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.contactName, contactName) && Equals(rhs.emailAddress, emailAddress) && Equals(rhs.phoneNumber, phoneNumber);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (VenueContact)) return false;
            return Equals((VenueContact) rhs);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (contactName != null ? contactName.GetHashCode() : 0);
                result = (result*397) ^ (emailAddress != null ? emailAddress.GetHashCode() : 0);
                result = (result*397) ^ (phoneNumber != null ? phoneNumber.GetHashCode() : 0);
                return result;
            }
        }

        public static bool operator ==(VenueContact left, VenueContact right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(VenueContact left, VenueContact right)
        {
            return !Equals(left, right);
        }
    }
}