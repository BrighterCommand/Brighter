using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using paramore.rewind.adapters.presentation.api.Translators;

namespace paramore.rewind.adapters.presentation.api.Resources
{
    public class SpeakerResource
    {
        private Link self;

        public SpeakerResource(Guid id, int version, string name, string phoneNumber, string emailAddress, string bio)
        {
            Version = version;
            Name = name;
            PhoneNumber = phoneNumber;
            EmailAddress = emailAddress;
            Bio = bio;

            Id = id;
            self = new Link(relName: ParamoreGlobals.Self, resourceName: "speaker", id: id.ToString());
            Links = new List<Link>{self};
        }

        public SpeakerResource() {}

        [XmlIgnore]
        public Guid Id { get; set; }
        [DataMember(Name = "version")]
        [XmlElement(ElementName = "version")]
        public int Version { get; set; }
        [DataMember(Name = "name")]
        [XmlElement(ElementName = "name")]
        public string Name { get; set; }
        [DataMember(Name = "phoneNumber")]
        [XmlElement(ElementName = "phoneNumber")]
        public string PhoneNumber { get; set; }
        [DataMember(Name = "emailAddress")]
        [XmlElement(ElementName = "emailAddress")]
        public string EmailAddress { get; set; }
        [DataMember(Name = "bio")]
        [XmlElement(ElementName = "bio")]
        public string Bio { get; set; }
        [DataMember(Name = "links")]
        [XmlArray(ElementName = "links")]
        public List<Link> Links { get; set; }

        public Link this[string linkName]
        {
            get
            {
                if (linkName == ParamoreGlobals.Self)
                {
                    return self;
                }
                else
                    throw new ArgumentOutOfRangeException("Unsupported link");
            }
        }
    }
}