using System;
using Fohjin.DDD.EventStore;
using Fohjin.DDD.EventStore.Aggregate;
using Fohjin.DDD.EventStore.Storage.Memento;
using UserGroupManagement.Domain.Common;
using UserGroupManagement.Domain.Momentos;
using UserGroupManagement.Events.Location;

namespace UserGroupManagement.Domain.Location
{
    public class Location : BaseAggregateRoot<IDomainEvent>, IOrginator
    {
        private LocationName locationName;
        private Address address;
        private LocationMap map;
        private LocationContact locationContact;

        public Location(LocationName locationName, Address address, LocationMap map, LocationContact locationContact)
            : this()
        {
            Apply(new LocationCreatedEvent(
                Guid.NewGuid(), 
                locationName.Name, 
                address.StreetNumber, 
                address.Street, 
                address.City, 
                address.PostalCode, 
                map.Map.OriginalString, 
                locationContact.ContactName.Name, 
                locationContact.EmailAddress.Email, 
                locationContact.PhoneNumber.Number));
        }

        public Location()
        {
            RegisterEvents();
        }

        public IMemento CreateMemento()
        {
            var memento = new LocationMemento(
                Id, 
                Version, 
                locationName.Name,
                address.StreetNumber,
                address.Street,
                address.City,
                address.PostalCode,
                map.Map.OriginalString,
                locationContact.ContactName.Name,
                locationContact.EmailAddress.Email,
                locationContact.PhoneNumber.Number);
            return memento;
        }

        public void SetMemento(IMemento memento)
        {
            return;
        }  
        
        private void RegisterEvents()
        {
            RegisterEvent<LocationCreatedEvent>(OnLocationCreated);
        }

        private void OnLocationCreated(LocationCreatedEvent locationCreatedEvent)
        {
            Id = locationCreatedEvent.Id;
            locationName = new LocationName(locationCreatedEvent.LocationName);
            address = new Address(locationCreatedEvent.StreetNumber, locationCreatedEvent.Street, locationCreatedEvent.City, locationCreatedEvent.PostalCode);
            map = new LocationMap(new Uri(locationCreatedEvent.Map));
            locationContact = new LocationContact(
                new ContactName(locationCreatedEvent.ContactName), 
                new EmailAddress(locationCreatedEvent.ContactEmail), 
                new PhoneNumber(locationCreatedEvent.ContactPhoneNumber));
        }
    }
}