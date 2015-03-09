using System;
using System.Data;
using FakeItEasy;
using Machine.Specifications;
using Paramore.Adapters.Tests.UnitTests.fakes;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Common;
using Paramore.Rewind.Core.Domain.Venues;
using Paramore.Rewind.Core.Ports.Commands.Venue;
using Paramore.Rewind.Core.Ports.Handlers.Venues;
using Version = Paramore.Rewind.Core.Adapters.Repositories.Version;

namespace Paramore.Adapters.Tests.UnitTests.services.CommandHandlers.Venues
{
    [Subject("Updating and existing venue")]
    public class When_updating_an_existing_venue
    {
        static UpdateVenueCommandHandler updateVenueCommandHandler;
        static UpdateVenueCommand updateVenueCommand;
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

            updateVenueCommand = new UpdateVenueCommand(
                id: VENUE_ID,
                venueName: "My Venue Name",
                address: "StreetNumber: 1, Street: MyStreet, City: London, PostCode: SW1 1PL",
                mapURN: "http://www.mysite.com/maps/12345",
                contact: "Name: Mary Alice, EmailAddress: mary.alice@foobar.com: , PhoneNumber: 0111 111 1111",
                version: 0);


            updateVenueCommandHandler = new UpdateVenueCommandHandler(venueRepository, uoWFactory);
        };

        static Venue GetVenueFromRepoBy(Guid id)
        {
            return venueRepository[id];
        }

        Because of = () => updateVenueCommandHandler.Handle(updateVenueCommand);

        It should_ask_the_session_factory_for_a_unit_of_work = () => A.CallTo(() => uoWFactory.CreateUnitOfWork()).MustHaveHappened();
        It should_commit_the_unit_of_work = () => A.CallTo(() => uow.Commit()).MustHaveHappened();
        It should_set_the_name_of_the_venue = () => GetVenueFromRepoBy(updateVenueCommand.Id).ToDocument().VenueName.ShouldEqual(updateVenueCommand.VenueName);
        It should_set_the_address_of_the_venue = () => GetVenueFromRepoBy(updateVenueCommand.Id).ToDocument().Address.ShouldEqual(updateVenueCommand.Address);
        It should_set_the_mapURN_for_the_venue = () => GetVenueFromRepoBy(updateVenueCommand.Id).ToDocument().VenueMap.ShouldEqual(updateVenueCommand.VenueMap);
        It should_set_the_contact_for_the_venue = () => GetVenueFromRepoBy(updateVenueCommand.Id).ToDocument().VenueContact.ShouldEqual(updateVenueCommand.Contact);
    }

    public class When_updating_a_venue_and_versions_dont_match  
    {
        static UpdateVenueCommandHandler updateVenueCommandHandler;
        static UpdateVenueCommand updateVenueCommand;
        static FakeRepository<Venue, VenueDocument> venueRepository;
        static IAmAUnitOfWorkFactory uoWFactory;
        static IUnitOfWork uow;
        static readonly Guid VENUE_ID = Guid.NewGuid();
        protected static Exception Exception; 

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

            updateVenueCommand = new UpdateVenueCommand(
                id: VENUE_ID,
                venueName: "My Venue Name",
                address: "StreetNumber: 1, Street: MyStreet, City: London, PostCode: SW1 1PL",
                mapURN: "http://www.mysite.com/maps/12345",
                contact: "Name: Mary Alice, EmailAddress: mary.alice@foobar.com: , PhoneNumber: 0111 111 1111",
                version: 7);


            updateVenueCommandHandler = new UpdateVenueCommandHandler(venueRepository, uoWFactory);
        };

        Because of = () => Exception = Catch.Exception(() => updateVenueCommandHandler.Handle(command: updateVenueCommand));

        It should_throw_an_exception_as_the_versions_do_not_match = () => Exception.Data.ShouldNotBeNull();
        It should_raise_an_optimistic_concurrency_exception = () => Exception.ShouldBeOfType<OptimisticConcurrencyException>();
    }
}
