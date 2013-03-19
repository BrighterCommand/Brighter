using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Serialization;
using Paramore.Adapters.Presentation.API.Handlers;
using Paramore.Adapters.Presentation.API.Translators;

namespace Paramore.Adapters.Presentation.API.Resources
{
    [XmlRoot]
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
            map = new Link(relName: ParamoreGlobals.Map, href: mapURN);
            Links = new List<Link>{self, map};

            this.Version = version;
            this.Name = name;
            this.Address = address;
            this.Contact = contact;
        }

        [XmlIgnore]
        public Guid Id { get; set; }
        [XmlElement(ElementName = "name")]
        public string Name { get; set; }
        [XmlElement(ElementName = "address")]
        public string Address { get; set; }
        [XmlElement(ElementName = "contact")]
        public string Contact { get; set; }
        [XmlElement(ElementName = "version")]
        public int Version { get; set; }
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