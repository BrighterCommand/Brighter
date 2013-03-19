using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using Paramore.Adapters.Presentation.API.Handlers;
using Paramore.Adapters.Presentation.API.Translators;

namespace Paramore.Adapters.Presentation.API.Resources
{
    [DataContract(Name = "Venue")]
    [XmlSerializerFormat]
    internal class VenueResource
    {
        public Link Self { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Address { get; set; }
        [DataMember]
        public Link Map { get; set; }
        [DataMember]
        public string Contact { get; set; }
        [DataMember]
        public int Version { get; set; }

        public VenueResource(Guid id, int version, string name, string address, string mapURN, string contact)
        {
            this.Self = new Link(relName: ParamoreGlobals.Self, resourceName: "venue", id: id.ToString());
            this.Map = new Link(relName: ParamoreGlobals.Map, href: mapURN);
            this.Version = version;
            this.Name = name;
            this.Address = address;
            this.Contact = contact;
        }

 
    }
}