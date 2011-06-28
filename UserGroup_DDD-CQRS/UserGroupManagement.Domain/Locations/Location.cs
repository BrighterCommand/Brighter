using System;
using UserGroupManagement.Domain.Common;
using UserGroupManagement.Infrastructure.Domain;

namespace UserGroupManagement.Domain.Locations
{
    public class Location : IAggregateRoot 
    {
        private LocationName locationName;
        private Address address;
        private LocationMap map;
        private LocationContact locationContact;

        private Guid _id;

        private int _version;

        public Location(LocationName locationName, Address address, LocationMap map, LocationContact locationContact)
            : this()
        {
  
        }

        public Location()
        {
        }

        public Guid SisoId
        {
            get { return _id; }
        }

        public int Version
        {
            get { return _version; }
        }

        public int Lock(int expectedVersion)
        {
            throw new NotImplementedException();
        }

        //private void OnLocationCreated(LocationCreatedEvent locationCreatedEvent)
        //{
        //    Id = locationCreatedEvent.Id;
        //    locationName = new LocationName(locationCreatedEvent.LocationName);
        //    address = new Address(locationCreatedEvent.StreetNumber, locationCreatedEvent.Street, locationCreatedEvent.City, locationCreatedEvent.PostalCode);
        //    map = new LocationMap(new Uri(locationCreatedEvent.Map));
        //    locationContact = new LocationContact(
        //        new ContactName(locationCreatedEvent.ContactName), 
        //        new EmailAddress(locationCreatedEvent.ContactEmail), 
        //        new PhoneNumber(locationCreatedEvent.ContactPhoneNumber));
        //}
    }
}