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
            self = new Link(relName: ParamoreGlobals.Self, resourceName: "venue", id: id.ToString());
            map = new Link(relName: ParamoreGlobals.Map, href: mapURN);
            Links = new List<Link>{self, map};

            this.Version = version;
            this.Name = name;
            this.Address = address;
            this.Contact = contact;
        }

        [XmlElement]
        public string Name { get; set; }
        [XmlElement]
        public string Address { get; set; }
        [XmlElement]
        public string Contact { get; set; }
        [XmlElement]
        public int Version { get; set; }
        [XmlArray(ElementName = "Links")]
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