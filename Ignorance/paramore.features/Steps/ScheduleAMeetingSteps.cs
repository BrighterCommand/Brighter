using System;
using System.Linq;
using NUnit.Framework;
using Paramore.Domain.Common;
using Paramore.Domain.Meetings;
using Paramore.Domain.Speakers;
using Paramore.Domain.Venues;
using Paramore.Features.Tools;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Paramore.Services.CommandHandlers;
using Paramore.Services.CommandHandlers.Meetings;
using Paramore.Services.CommandProcessors;
using Paramore.Services.Commands.Meeting;
using Raven.Client.Linq;
using TechTalk.SpecFlow;
using TinyIoC;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Features.Steps
{
    [Binding]
    public class ScheduleAMeetingSteps
    {
        private readonly ScheduleMeetingCommand scheduleMeetingCommand = new ScheduleMeetingCommand();
        private static CommandProcessor commandProcessor;
        private static TinyIoCContainer container;
        private Id speakerId;
        private Id venueId;
        private MeetingDTO scheduledMeeting;

        [BeforeFeature]
        public static void SetUp()
        {
            container = new TinyIoCContainer();

            commandProcessor = new CommandProcessor(container);
            container.Register<IAmAUnitOfWorkFactory, UnitOfWorkFactory>().AsSingleton();
            container.Register<IRepository<Meeting, MeetingDTO>, Repository<Meeting, MeetingDTO>>().AsMultiInstance();
            container.Register<IIssueTickets, TicketIssuer>().AsMultiInstance();
            container.Register<IScheduler, Scheduler>();
            container.Register<IAmAnOverbookingPolicy, FiftyPercentOverbookingPolicy>();
            container.Register<IIssueTickets, TicketIssuer>();
            container.Register<IHandleRequests<ScheduleMeetingCommand>, ScheduleMeetingCommandHandler>("ScheduleMeetingCommandHandler");
        }

        [Given(@"I have a speaker (.*)")]
        public void GivenIHaveASpeaker(string speakerName)
        {
            using (var uow = container.Resolve<IAmAUnitOfWorkFactory>().CreateUnitOfWork())
            {
                speakerId = new Id(Guid.NewGuid());
                uow.Add(new SpeakerDTO(
                            speakerId,
                            new Version(),
                            new SpeakerBio("Augusta Ada King, Countess of Lovelace (10 December 1815 – 27 November 1852), born Augusta Ada Byron, was an English writer chiefly known for her work on Charles Babbage's early mechanical general-purpose computer, the analytical engine."),
                            new PhoneNumber("888-888-8888"),
                            new EmailAddress("augusta@lovelace.org"),
                            new Name("Augusta Ada King, Countess of Lovelace")));
                uow.Commit();
            }

            scheduleMeetingCommand.SpeakerId = speakerId;
        }

        [Given(@"I have a venue (.*)")]
        public void GivenIHaveAVenue(string venueName)
        {
            using (var uow = container.Resolve<IAmAUnitOfWorkFactory>().CreateUnitOfWork())
            {
                venueId = new Id(Guid.NewGuid());
                var venue = new VenueDTO(
                    venueId, 
                    new Version(), 
                    new VenueName("The American Bar, The Savoy Hotel"),
                    new Address(new Street("The Strand"), new City("London"), new PostCode("WC2R 0EU")),
                    new VenueMap(new Uri("http://www.fairmont.com/savoy/MapAndDirections.htm")),
                    new VenueContact(new ContactName("Athena Cripps"), new EmailAddress("Athena.Cripps@fairmont.com"), new PhoneNumber(" +44 (0)20 7420 2492")));
                uow.Add(venue);
                uow.Commit();
            }
            scheduleMeetingCommand.VenueId = venueId;
        }

        [Given(@"I have a meeting date (.*)")]
        public void GivenIHaveAMeetingDate(string dateOfMeeting)
        {
            scheduleMeetingCommand.On = FuzzyDateTime.Parse(dateOfMeeting);
        }

        [Given(@"I have a capacity of (\d+)")]
        public void GivenIHaveACapacityOf(int seats)
        {
            scheduleMeetingCommand.Capacity = seats;
        }
 

        [When(@"I schedule a meeting")]
        public void WhenIScheduleAMeeting()
        {
            scheduleMeetingCommand.MeetingId = Guid.NewGuid();
            commandProcessor.Send(scheduleMeetingCommand);
        }

        [Then(@"the new meeting should be open for registration")]
        public void ThenTheNewMeetingShouldBeOpenForRegistration()
        {
            using (var uow = container.Resolve<IAmAUnitOfWorkFactory>().CreateUnitOfWork())
            {
                var results = uow.Query<MeetingDTO>()
                    .Where(meeting => meeting.Id == scheduleMeetingCommand.MeetingId)
                    .Customize(x => x.WaitForNonStaleResults())
                    .ToList();
                scheduledMeeting = results.First();
                Assert.That(scheduledMeeting.State == MeetingState.Live);
            }
        }

        [Then(@"the date should be (.*)")]
        public void ThenTheDateShouldBe(string dateOfMeeting)
        {
            Assert.That(DateTime.Parse(dateOfMeeting), Is.EqualTo(scheduledMeeting.MeetingDate));
        }

        [Then(@"(.*) tickets should be available")]
        public void ThenTicketsShouldBeAvailable(int noOfTickets)
        {
            Assert.That(noOfTickets, Is.EqualTo(scheduledMeeting.Tickets.Count()));
        }


    }
}
