using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace paramore.rewind.adapters.presentation.api.Resources
{
    [XmlRoot]
    [DataContract]
    public class ContactResource
    {

        [XmlElement(ElementName = "name")]
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [XmlElement(ElementName = "emailAddress")]
        [DataMember(Name = "emailAddress")]
        public string EmailAddress { get; set; }
        [XmlElement(ElementName = "phoneNumber")]
        [DataMember(Name = "phoneNumber")]
        public string PhoneNumber { get; set; }

        public ContactResource()
        {
            //required for serialization
        }

        public ContactResource(string name, string emailAddress, string phoneNumber)
        {
            Name = name;
            EmailAddress = emailAddress;
            PhoneNumber = phoneNumber;
        }

        public static ContactResource Parse(string contact)
        {
           var rx = new Regex("Name: (.*), EmailAddress: (.*), PhoneNumber: (.*)");
            var match = rx.Match(contact);
            var name = match.Groups[1].Value;
            var emailAddress = match.Groups[2].Value;
            var phoneNumber = match.Groups[3].Value;
            return new ContactResource(name, emailAddress, phoneNumber);
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, EmailAddress: {1}, PhoneNumber: {2}", Name, EmailAddress, PhoneNumber);
        }

        public static implicit operator string(ContactResource contactResource)
        {
            return contactResource.ToString();
        }
    }
}