using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace paramore.rewind.adapters.presentation.api.Resources
{
    [XmlRoot]
    [DataContract]
    public class AddressResource
    {
        [XmlElement(ElementName = "buildingNumber")]
        [DataMember(Name = "buildingNumber")]
        public string StreetNumber { get; set; }
        [XmlElement(ElementName = "streetName")]
        [DataMember(Name = "streetName")]
        public string Street { get; set; }
        [XmlElement(ElementName = "city")]
        [DataMember(Name = "city")]
        public string City { get; set; }
        [XmlElement(ElementName = "postCode")]
        [DataMember(Name = "postCode")]
        public string Postcode { get; set; }

        public AddressResource()
        {
            //required for serialization
        }

        public AddressResource(string streetNumber, string street, string city, string postcode)
        {
            StreetNumber = streetNumber;
            Street = street;
            City = city;
            Postcode = postcode;
        }

        public static AddressResource Parse(string address)
        {
            var rx = new Regex("Street: BuildingNumber: (.*), StreetName: (.*), City: (.*), PostCode: (.*)");
            var match = rx.Match(address);
            var streetNumber = match.Groups[1].Value;
            var street = match.Groups[2].Value;
            var city = match.Groups[3].Value;
            var postcode = match.Groups[4].Value;

            return new AddressResource(streetNumber, street, city, postcode);
        }

        public override string ToString()
        {
            return string.Format("Street: BuildingNumber: {0}, StreetName: {1}, City: {2}, PostCode: {3}", StreetNumber, Street, City, Postcode);
        }

        public static implicit operator string(AddressResource resource)
        {
            return resource.ToString();
        }
    }
}