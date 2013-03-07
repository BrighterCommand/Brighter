using System;
using Machine.Specifications;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Adapters.Presentation.API.Handlers;
using Paramore.Adapters.Presentation.API.Resources;
using Paramore.Adapters.Presentation.API.Translators;
using Paramore.Domain.Common;
using Paramore.Domain.Venues;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace paramore.integrationtests.Translators
{
    [Subject("Chekc that we can get the venue list out of the thin read layer")]
    public class When_changing_a_document_to_a_resource
    {
        private static readonly VenueTranslator venueTranslator = new VenueTranslator();
        private static VenueDocument document;
        private static VenueResource resource;

        Establish context = () =>
            {
                document = new VenueDocument(
                    id: new Id(Guid.NewGuid()),
                    version: new Version(1),
                    venueName: new VenueName("Test Venue"),
                    address: new Address(new Street("MyStreet"), new City("London"), new PostCode("N1 3GA")),
                    venueMap: new VenueMap(new Uri("http://www.mysite.com/maps/12345")),
                    venueContact: new VenueContact(new ContactName("Ian"), new EmailAddress("ian@huddle.com"), new PhoneNumber("123454678")));

            };

        Because of = () => resource = venueTranslator.Translate(document);

        It should_set_the_self_uri = () => resource.Self.ToString().ShouldEqual(string.Format("<link rel='self' href='//{0}/venue/{1}'>", ParamoreGlobals.HostName, document.Id));
        It should_set_the_map_link = () => resource.Map.ToString().ShouldEqual(string.Format("<link rel='map' href='{0}'>", document.VenueMap));
    }
}
