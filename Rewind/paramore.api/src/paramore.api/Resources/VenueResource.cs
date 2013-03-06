using System;
using System.Runtime.Serialization;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Venues;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Adapters.Presentation.API.Handlers
{
    [DataContract(Name = "Venue")]
    internal class VenueResource
    {
        public Uri Self { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Address { get; set; }
        [DataMember]
        public Uri Map { get; set; }
        [DataMember]
        public string Contact { get; set; }
        [DataMember]
        public int Version { get; set; }

        public VenueResource(Guid id, int version, string name, string address, string map, string contact)
        {
            this.Version = version;
            this.Name = name;
            this.Address = address;
            this.Contact = contact;
        }

    }
}