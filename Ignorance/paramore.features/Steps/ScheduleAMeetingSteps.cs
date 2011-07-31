using System;
using Castle.Windsor;
using Castle.MicroKernel.Registration;
using Paramore.Features.Tools;
using Paramore.Services.CommandHandlers;
using Paramore.Services.CommandProcessors;
using Paramore.Services.Commands.Meeting;
using TechTalk.SpecFlow;

namespace Paramore.Features.Steps
{
    [Binding]
    public class ScheduleAMeetingSteps
    {
        private readonly ScheduleMeetingCommand scheduleMeetingCommand = new ScheduleMeetingCommand(Guid.NewGuid());
        private CommandProcessor commandProcessor;

        [BeforeFeature]
        public void SetUp()
        {
            var container = new WindsorContainer();
            container.Register(Component.For<IHandleRequests<ScheduleMeetingCommand>>().ImplementedBy<ScheduleMeetingCommandHandler>());
            commandProcessor = new CommandProcessor(container);
        }

        [Given(@"I have a speaker (.*)")]
        public void GivenIHaveASpeaker(string speakerName)
        {
            //lookup the speaker - just use SQL to do this, via a thin read layer - grab the Id
        }

        [Given(@"I have a venue (.*)")]
        public void GivenIHaveAVenue(string venueName)
        {
            //lookkup the venue - just use SQL to do this, via a thin read layer - grab the id
        }

        [Given(@"I have a meeting date (.*)")]
        public void GivenIHaveAMeetingDate(string dateOfMeeting)
        {
            scheduleMeetingCommand.On = FuzzyDateTime.Parse(dateOfMeeting);
        }

        [Given(@"I have a capacity ((\d+))")]
        public void GivenIHaveACapacity(int seats)
        {
            scheduleMeetingCommand.Capacity = seats;
        }
 

        [When(@"I schedule a meeting")]
        public void WhenIScheduleAMeeting()
        {
            

            //new DomainDatabaseBootStrapper().ReCreateDatabaseSchema();

            //var sqliteConnectionString = string.Format("Data Source={0}", DATA_BASE_FILE);

            commandProcessor.Send(scheduleMeetingCommand);

            //how do we publish to report, directly or via command handler. Looks like by using transaction handler we go through unit of work whose commit method fires events to BUS
            //so if we have event and then save they get re-ublished and report canpick up
 
        }

        [Then(@"the new meeting should be open for registration")]
        public void ThenTheNewMeetingShouldBeOpenForRegistration()
        {
            //var sut = reportingRepository.GetByExample<MeetingDetailsReport>(new { MeetingTime = meetingDate }).FirstOrDefault();
        }


    }
}
