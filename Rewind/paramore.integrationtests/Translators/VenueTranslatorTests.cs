using System;
using System.IO;
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
                    venueContact: new VenueContact(new ContactName("Ian"), new EmailAddress("ian@huddle.com"), new PhoneNumber("123454678")));

            };

        Because of = () => resource = venueTranslator.Translate(document);

        It should_set_the_self_uri = () => resource[ParamoreGlobals.Self].ToString().ShouldEqual(string.Format("<link rel=\"self\" href=\"//{0}/venue/{1}\" />", ParamoreGlobals.HostName, document.Id));
        It should_set_the_map_link = () => resource[ParamoreGlobals.Map].ToString().ShouldEqual(string.Format("<link rel=\"map\" href=\"{0}\" />", document.VenueMap));
        It should_set_the_version = () => resource.Version.ShouldEqual(document.Version);
        It should_set_the_venue_name = () => resource.Name.ShouldEqual(document.VenueName);
        It should_set_the_address = () => resource.Address.ShouldEqual(document.Address);
        It should_set_the_contact = () => resource.Contact.ShouldEqual(document.VenueContact);

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

        It should_format_the_self_uri_as_expected = () => response.ShouldContain(string.Format("<link rel=\"self\" href=\"//{0}/venue/{1}\" />", ParamoreGlobals.HostName, resource.Id));
        It should_format_the_map_uri_as_expected = () => response.ShouldContain(string.Format("<link rel=\"map\" href=\"{0}\" />", resource.MapURN));
        It should_format_the_venue_name_as_expected = () => response.ShouldContain(string.Format("<name>{0}</name>", resource.Name));
        It should_format_the_address_as_expected = () => response.ShouldContain(string.Format("<address>{0}</address>", resource.Address));
        It should_format_the_contact_as_expected = () => response.ShouldContain(string.Format("<contact>{0}</contact>", resource.Contact));
        It should_format_the_version_as_expected = () => response.ShouldContain(string.Format("<version>{0}</version>", resource.Version)); 
    }
}
