using System;
using System.Linq;
using NUnit.Framework;
using Paramore.Domain.Venues;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Paramore.Services.CommandHandlers.Venues;
using Paramore.Services.Commands.Venue;
using Paramore.Services.ThinReadLayer;
using TechTalk.SpecFlow;
using TinyIoC;
using paramore.commandprocessor;
using paramore.commandprocessor.ioccontainers.IoCContainers;

namespace Paramore.Features.Steps
{
    [Binding]
    public class AddAVenue
    {
        private static IAmAnInversionOfControlContainer container;
        private static CommandProcessor commandProcessor;
        private static AddVenueCommand command;
        private readonly Guid newVenueId = Guid.NewGuid();

        [BeforeFeature]
        public static void SetUp()
        {
            command = new AddVenueCommand();

            container = new TinyInversionOfControlContainer(new TinyIoCContainer());

            commandProcessor = new CommandProcessor(container);
            container.Register<IAmAUnitOfWorkFactory, UnitOfWorkFactory>().AsSingleton();
            container.Register<IRepository<Venue, VenueDTO>, Repository<Venue, VenueDTO>>().AsMultiInstance();
            container.Register<IHandleRequests<AddVenueCommand>, AddVenueCommandHandler>("ScheduleMeetingCommandHandler");
        }

        [Given(@"That I have a venue name of (.*)")]
        public void GivenThatIHaveAVenueNameOf(string venueName)
        {
            command.VenueName = venueName;
        }

        [When(@"I create a new venue")]
        public void WhenICreateANewVenue()
        {
            command.Id = newVenueId;
            commandProcessor.Send(command);
        }

        [Then(@"whe I list venues (.*) should be included")]
        public void ThenWheIListVenuesShouldBeIncluded(string venueName)
        {
            var reader = new VenueReader(unitOfWorkFactory: container.Resolve<IAmAUnitOfWorkFactory>());
            var venues = reader.GetAll().ToList();
            Assert.IsTrue(venues.Where(venue => venue.Id == newVenueId).Any());
        }
    }

    [Binding]
    public class AddAnAddressToAVenue
    {
        [Given(@"I have a street address of (.*)")]
        public void GivenIHaveAStreetAddressOf(string streetAddress)
        {
            ScenarioContext.Current.Pending();
        }

        [Given(@"a city of (.*)")]
        public void GivenACityOf(string city)
        {
            ScenarioContext.Current.Pending();
        }

        [Given(@"a post code of (.*)")]
        public void GivenAPostCodeOf(string postCode)
        {
            ScenarioContext.Current.Pending();
        }

        [When(@"I add an address to a venue and ask for directions")]
        public void WhenIAddAnAddressToAVenueAndAskForDirections()
        {
            ScenarioContext.Current.Pending();
        }

        [Then(@"I should get a street address of (.*)")]
        public void ThenIShouldGetAStreetAddressOf(string streetAddress)
        {
            ScenarioContext.Current.Pending();
        }

        [Then(@"a city of (.*)")]
        public void ThenACityOf(string city)
        {
            ScenarioContext.Current.Pending();
        }

        [Then(@"a post code of (.*)")]
        public void ThenAPostCodeOf(string postCode)
        {
            ScenarioContext.Current.Pending();
        }
    }

    [Binding]
    public class AddAMapToAVenue
    {
        [Given(@"I have a map uri of (.*)")]
        public void GivenIHaveAMapUriOfHttpSkillsmatter_ComGoFind_Us(string mapUri)
        {
            ScenarioContext.Current.Pending();
        }

        [When(@"I add a map to a location and ask for directions")]
        public void WhenIAddAMapToALocationAndAskForDirections()
        {
            ScenarioContext.Current.Pending();
        }


        [Then(@"I should get a map uri of (.*)")]
        public void ThenIShouldGetAMapUriOfHttpSkillsmatter_ComGoFind_Us(string mapUri)
        {
            ScenarioContext.Current.Pending();
        }
    }

}
