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

        public static implicit operator string(VenueContact venueContact)
        {
            return venueContact.ToString();
        }

        public override string ToString()
        {
            return string.Format("Name : {0}, EmailAddress {1}: , PhoneNumber : {2}", contactName, emailAddress, phoneNumber);
        }
    }
}