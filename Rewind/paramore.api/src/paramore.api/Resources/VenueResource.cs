using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Paramore.Adapters.Presentation.API.Translators;

namespace Paramore.Adapters.Presentation.API.Resources
{
    [XmlRoot]
    [DataContract]
    public class VenueResource
    {
        private Link self;
        private Link map;

        public VenueResource()
        {
            //Required for serialization
            Links = new List<Link>();
        }

        public VenueResource(Guid id, int version, string name, string address, string mapURN, string contact)
        {
            Id = id;
            self = new Link(relName: ParamoreGlobals.Self, resourceName: "venue", id: id.ToString());
            MapURN = mapURN;
            map = new Link(relName: ParamoreGlobals.Map, href: mapURN);
            Links = new List<Link>{self, map};

            this.Version = version;
            this.Name = name;
            this.Address = AddressResource.Parse(address);
            this.Contact = ContactResource.Parse(contact);
        }

        [XmlIgnore]
        public Guid Id { get; set; }
        [XmlIgnore]
        public string MapURN { get; set; }
        [XmlElement(ElementName = "name")]
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "address")]
        [XmlElement(ElementName = "address")]
        public AddressResource Address { get; set; }
        [DataMember(Name = "contact")]
        [XmlElement(ElementName = "contact")]
        public ContactResource Contact { get; set; }
        [DataMember(Name = "version")]
        [XmlElement(ElementName = "version")]
        public int Version { get; set; }
        [DataMember(Name = "links")]
        [XmlArray(ElementName = "links")]
        public List<Link> Links { get; set; }

        public Link this[string linkName]
        {
            get
            {
                if (linkName == ParamoreGlobals.Self)
                    return self;
                else if (linkName == ParamoreGlobals.Map)
                    return map;
                else
                    throw new ArgumentOutOfRangeException("Unsupported link");

            }
        }
    }
}