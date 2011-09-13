using System.Text.RegularExpressions;
using Paramore.Domain.Common;

namespace Paramore.Domain.Venues
{
    public class VenueContact
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

        public static implicit operator string(VenueContact venueContact)
        {
            return venueContact.ToString();
        }

        public override string ToString()
        {
            return string.Format("Name : {0}, EmailAddress : {1}: , PhoneNumber : {2}", contactName, emailAddress, phoneNumber);
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
    }
}