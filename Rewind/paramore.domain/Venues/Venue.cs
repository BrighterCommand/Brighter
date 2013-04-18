using System;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Common;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Domain.Venues
{
    public class Venue : AggregateRoot<VenueDocument> 
    {
        private Address address;
        private Contact contact;
        private VenueMap map;
        private VenueName name;

        public Venue(Version version, VenueName name, Address address, VenueMap map, Contact contact) 
            : this(new Id(), version, name, address, map, contact){}

        public Venue(Id id, Version version, VenueName venueName) 
            : this(id, version, venueName, new Address(), new VenueMap(), new Contact()) {}

        public Venue(Id id, Version version, VenueName name, Address address, VenueMap map, Contact contact) : base(id, version)
        {
            this.address = address;
            this.contact = contact;
            this.map = map;
            this.name = name;
        }

        public Venue() : base(new Id(), new Version()){}


        #region Aggregate  Persistence
        public override void Load(VenueDocument document)
        {
            address = Address.Parse(document.Address); 
            contact = Contact.Parse(document.VenueContact);
            map = new VenueMap(new Uri(document.VenueMap));
            name = new VenueName(document.VenueName);
        }

        public override VenueDocument ToDocument()
        {
            return new VenueDocument(id, version, name, address, map, contact);
        }

        #endregion

        public override string ToString()
        {
            return string.Format("Address: {0}, Contact: {1}, Map: {2}, Name: {3}", address, contact, map, name);
        }
    }
}