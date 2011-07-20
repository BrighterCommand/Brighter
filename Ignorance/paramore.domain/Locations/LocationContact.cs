using Paramore.Domain.Common;

namespace Paramore.Domain.Locations
{
    public class LocationContact
    {
        public ContactName ContactName { get; private set; }
        public EmailAddress EmailAddress { get; private set; }
        public PhoneNumber PhoneNumber { get; private set; }

        public LocationContact(ContactName contactName, EmailAddress emailAddress, PhoneNumber phoneNumber)
        {
            ContactName = contactName;
            EmailAddress = emailAddress;
            PhoneNumber = phoneNumber;
        }
    }
}