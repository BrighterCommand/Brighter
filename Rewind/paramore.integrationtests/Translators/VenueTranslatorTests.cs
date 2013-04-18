using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Machine.Specifications;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Adapters.Presentation.API.Resources;
using Paramore.Adapters.Presentation.API.Translators;
using Paramore.Domain.Common;
using Paramore.Domain.Venues;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace paramore.integrationtests.Translators
{
    [Subject("We wantt to convert an address string into a hierachy to serialize it")]
    public class When_serializing_an_address_to_a_resource
    {
        static AddressResource resource;
        const string streetNumber = "123";
        const string street = "Sesame Street";
        const string city = "New York";
        const string postcode = "10128";

        Because of = () => resource = AddressResource.Parse(string.Format("Street: StreetNumber: {0}, Street: {1}, City : {2}, PostCode : {3}", streetNumber, street, city, postcode));

        It should_have_a_matching_streetNumber = () => resource.StreetNumber.ShouldEqual(streetNumber);
        It should_have_a_matching_street = () => resource.Street.ShouldEqual(street);
        It shoud_have_a_matching_city = () => resource.City.ShouldEqual(city);
        It should_have_a_matching_postcode = () => resource.Postcode.ShouldEqual(postcode);
    }

    [Subject("We want to convert a contact string into a hierachy to serialize it")]
    public class When_serializing_a_contact_to_a_resource
    {
        static ContactResource resource;
        const string name = "Mary Alice";
        const string emailAddress = "mary.alice@foobar.com";
        const string phoneNumber = "012345678";

        Because of = () => resource = ContactResource.Parse(string.Format("Name: {0}, EmailAddress: {1}, PhoneNumber: {2}", name, emailAddress, phoneNumber));

        It should_have_a_matching_name = () => resource.Name.ShouldEqual(name);
        It should_have_a_matching_emailAddress = () => resource.EmailAddress.ShouldEqual(emailAddress);
        It should_have_a_matching_phonenumber = () => resource.PhoneNumber.ShouldEqual(phoneNumber);
    }

    [Subject("Check that we can get the venue list out of the thin read layer")]
    public class When_changing_a_document_to_a_resource
    {
        static readonly VenueTranslator venueTranslator = new VenueTranslator();
        static VenueDocument document;
        static VenueResource resource;

        Establish context = () =>
            {
                document = new VenueDocument(
                    id: new Id(Guid.NewGuid()),
                    version: new Version(1),
                    venueName: new VenueName("Test Venue"),
                    address: new Address(new Street("MyStreet"), new City("London"), new PostCode("N1 3GT")),
                    venueMap: new VenueMap(new Uri("http://www.mysite.com/maps/12345")),
                    contact: new Contact(new Name("Ian"), new EmailAddress("ian@huddle.com"), new PhoneNumber("123454678")));

            };

        Because of = () => resource = venueTranslator.Translate(document);

        It should_set_the_self_uri = () => resource[ParamoreGlobals.Self].ToString().ShouldEqual(string.Format("<link rel=\"self\" href=\"http://{0}/venue/{1}\" />", ParamoreGlobals.HostName, document.Id));
        It should_set_the_map_link = () => resource[ParamoreGlobals.Map].ToString().ShouldEqual(string.Format("<link rel=\"map\" href=\"{0}\" />", document.VenueMap));
        It should_set_the_version = () => resource.Version.ShouldEqual(document.Version);
        It should_set_the_venue_name = () => resource.Name.ShouldEqual(document.VenueName);
        It should_set_the_address = () => resource.Address.ToString().ShouldEqual(document.Address);
        It should_set_the_contact = () => resource.Contact.ToString().ShouldEqual(document.VenueContact);

    }

    [Subject("Check that we serialize to the expected xml")]
    public class When_serializing_a_resource_to_xml
    {
        static XmlSerializer serializer;
        static StringWriter stringwriter;
        static VenueResource resource;
        static string response;

        Establish context = () =>
            {
                stringwriter = new StringWriter();
                resource = new VenueResource(
                    id: Guid.NewGuid(),
                    version: 1,
                    name: "Test Venue",
                    address: "Street : StreetNumber: , Street: MyStreet, City : London, PostCode : N1 3GT",
                    mapURN: "http://www.mysite.com/maps/12345",
                    contact: "ContactName: Ian, EmailAddress: ian@huddle.com, PhoneNumber: 123454678"
                );
                serializer = new XmlSerializer(typeof(VenueResource));
            };

        Because of = () =>
            {
                using (var writer = new XmlTextWriter(stringwriter) { Formatting = Formatting.Indented })
                {
                    serializer.Serialize(writer, resource);
                }
                response =  stringwriter.GetStringBuilder().ToString();
            };

        It should_format_the_self_uri_as_expected = () => response.ShouldContain(string.Format("<link rel=\"self\" href=\"http://{0}/venue/{1}\" />", ParamoreGlobals.HostName, resource.Id));
        It should_format_the_map_uri_as_expected = () => response.ShouldContain(string.Format("<link rel=\"map\" href=\"{0}\" />", resource.MapURN));
        It should_format_the_venue_name_as_expected = () => response.ShouldContain(string.Format("<name>{0}</name>", resource.Name));
        It should_format_the_address_as_expected = () => response.ShouldContain(string.Format("<address>{0}</address>", resource.Address));
        It should_format_the_contact_as_expected = () => response.ShouldContain(string.Format("<contact>{0}</contact>", resource.Contact));
        It should_format_the_version_as_expected = () => response.ShouldContain(string.Format("<version>{0}</version>", resource.Version)); 
    }

    [Subject("Check writing a resource to JSON")]
    public class When_serializing_a_resource_to_JSON
    {
        static DataContractJsonSerializer serializer;
        static VenueResource resource;
        static string response;

        Establish context = () =>
        {
            resource = new VenueResource(
                id: Guid.NewGuid(),
                version: 1,
                name: "Test Venue",
                address: "Street : StreetNumber: , Street: MyStreet, City : London, PostCode : N1 3GT",
                mapURN: "http://www.mysite.com/maps/12345",
                contact: "ContactName: Ian, EmailAddress: ian@huddle.com, PhoneNumber: 123454678"
            );
            serializer = new DataContractJsonSerializer(typeof(VenueResource));
        };

        Because of = () =>
        {
            using (var memoryStream = new MemoryStream())
            {
                serializer.WriteObject(memoryStream, resource);
                response = Encoding.Default.GetString(memoryStream.ToArray());
            }
        };

        It should_not_be_null = () => response.ShouldNotBeNull();
    }

    [Subject("Check loading a resource from JSON")]
    public class When_serializing_a_resource_from_JSON
    {
        static string jsonData;
        static DataContractJsonSerializer serializer;
        private static VenueResource resource;

        Establish context = () =>
        {
            jsonData = "{\"address\":{\"city\":\"\",\"postCode\":\"\",\"street\":\"\",\"streetnumber\":\"\"},\"contact\":{\"emailAddress\":\"ian@huddle.com\",\"name\":\"Ian\",\"phoneNumber\":\"123454678\"},\"links\":[{\"HRef\":\"\\/\\/localhost:59280\\/venue\\/cc7519cf-d58e-4e9c-a340-855254f67de5\",\"Rel\":\"self\"},{\"HRef\":\"http:\\/\\/www.mysite.com\\/maps\\/12345\",\"Rel\":\"map\"}],\"name\":\"Test Venue\",\"version\":1}";
            serializer = new DataContractJsonSerializer(typeof(VenueResource));
        };

        Because of = () =>
        {
            using (var memoryStream = new MemoryStream(Encoding.Unicode.GetBytes(jsonData)))
            {
                resource = (VenueResource)serializer.ReadObject(memoryStream);
            }
        };

        It should_deserialize_the_venue = () => resource.ShouldNotBeNull();
    }
}
