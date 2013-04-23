using System;
using System.Data;
using FakeItEasy;
using Machine.Specifications;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Adapters.Tests.UnitTests.fakes;
using Paramore.Domain.Common;
using Paramore.Domain.Venues;
using Paramore.Ports.Services.Commands.Venue;
using Paramore.Ports.Services.Handlers.Venues;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Adapters.Tests.UnitTests.services.CommandHandlers.Venues
{
    [Subject("When deleting a venue")]
    public class When_deleting_a_venue
    {
        static DeleteVenueCommandHandler deleteVenueCommandHandler;
        static DeleteVenueCommand deleteVenueCommand;
        static FakeRepository<Venue, VenueDocument> venueRepository;
        static IAmAUnitOfWorkFactory uoWFactory;
        static IUnitOfWork uow;
        static readonly Guid VENUE_ID = Guid.NewGuid();

        Establish context = () =>
        {
           venueRepository = new FakeRepository<Venue, VenueDocument>(); 
            venueRepository.Add(new Venue(
                id: new Id(VENUE_ID),
                version: new Version(),
                name: new VenueName("Hooper's Store"),
                address: new Address(new Street(123, "Sesame Street"), new City("New York"), new PostCode("19820")  ), 
                map: new VenueMap(new Uri("http://googlemaps.co.uk")), 
                contact: new Contact(new Name("Elmo"), new EmailAddress("elmo@childrenstvworkshop.com"), new PhoneNumber("12345678"))
                ));

            uoWFactory = A.Fake<IAmAUnitOfWorkFactory>();
            uow = A.Fake<IUnitOfWork>();

            A.CallTo(() => uoWFactory.CreateUnitOfWork()).Returns(uow);

            deleteVenueCommand = new DeleteVenueCommand(VENUE_ID);

            deleteVenueCommandHandler = new DeleteVenueCommandHandler(venueRepository, uoWFactory);
        };

        Because of = () => deleteVenueCommandHandler.Handle(deleteVenueCommand);

        static Venue GetVenueFromRepoBy(Guid id)
        {
            return venueRepository[id];
        }

        It should_no_longer_be_in_the_repository = () => GetVenueFromRepoBy(deleteVenueCommand.Id).ShouldBeNull();
        It should_ask_the_session_factory_for_a_unit_of_work = () => A.CallTo(() => uoWFactory.CreateUnitOfWork()).MustHaveHappened();
        It should_commit_the_unit_of_work = () => A.CallTo(() => uow.Commit()).MustHaveHappened();
    }
}
