using System;
using Paramore.Services.Events.Speaker;

namespace Paramore.Services.Events.Location
{
    public class LocationCreatedEvent : IDomainEvent
    {
        public Guid LocationId { get; private set; }
        public string LocationName { get; private set; }
        public string StreetNumber { get; private set; }
        public string Street { get; private set; }
        public string City { get; private set; }
        public string PostalCode { get; private set; }
        public string Map { get; private set; }
        public string ContactName { get; private set; }
        public string ContactEmail { get; private set; }
        public string ContactPhoneNumber { get; private set; }
 
        public LocationCreatedEvent(
            Guid locationId, 
            string locationName, 
            string streetNumber, 
            string street, 
            string city, 
            string postalCode, 
            string map, 
            string contactName, 
            string contactEmail, 
            string contactPhoneNumber)
        {
            LocationId = locationId;
            LocationName = locationName;
            StreetNumber = streetNumber;
            Street = street;
            City = city;
            PostalCode = postalCode;
            Map = map;
            ContactName = contactName;
            ContactEmail = contactEmail;
            ContactPhoneNumber = contactPhoneNumber;
        }
    }
}