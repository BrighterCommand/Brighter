using System;
using System.Xml.Serialization;
using Paramore.Adapters.Presentation.API.Translators;

namespace Paramore.Adapters.Presentation.API.Resources
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
        }

        [XmlIgnore]
        public Guid Id { get; set; }
        public int Version { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string EmailAddress { get; set; }
        public string Bio { get; set; }

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