using System;
using Fohjin.DDD.EventStore.Storage.Memento;

namespace UserGroupManagement.Domain.Momentos
{
    public class LocationMemento : IMemento
    {
        internal Guid Id { get; private set; }
        internal int Version { get; private set; }
        internal string LocationName { get; private set; }
        public string LocationStreetNumber { get; private set; }
        public string LocationStreet { get; private set; }
        public string LocationCity { get; private set; }
        public string LocationPostalCode { get; private set; }
        public string Map { get; private set; }
        public string ContactName { get; private set; }
        public string ContactEmail { get; private set; }
        public string ContactPhoneNumber { get; private set; }

        public LocationMemento(
            Guid id, 
            int version, 
            string locationName, 
            string locationStreetNumber, 
            string locationStreet, 
            string locationCity, 
            string locationPostalCode, 
            string map, 
            string contactName, 
            string contactEmail, 
            string contactPhoneNumber)
        {
            Id = id;
            Version = version;
            LocationName = locationName;
            LocationStreetNumber = locationStreetNumber;
            LocationStreet = locationStreet;
            LocationCity = locationCity;
            LocationPostalCode = locationPostalCode;
            Map = map;
            ContactName = contactName;
            ContactEmail = contactEmail;
            ContactPhoneNumber = contactPhoneNumber;
        }
    }
}