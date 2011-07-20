using System;
using TechTalk.SpecFlow;

namespace Paramore.Features.Steps
{
    [Binding]
    public class ScheduleAMeetingSteps
    {
        private Guid speakerId;
        private Guid locationId;
        private DateTime meetingDate;
        private int capacity;
        private static readonly Guid MEETING_ID = Guid.NewGuid();
        private const string DATA_BASE_FILE = "domainDataBase.db3";


        [Given(@"I have a speaker")]
        public void GivenIHaveASpeaker()
        {
            speakerId = Guid.NewGuid();
        }

        [Given(@"I have a venue")]
        public void GivenIHaveAVenue()
        {
            locationId = Guid.NewGuid();
        }

        [Given(@"I have a meeting date")]
        public void GivenIHaveAMeetingDate()
        {
            meetingDate = DateTime.Now;
        }

        [Given(@"I have a capacity")]
        public void GivenIHaveACapacity()
        {
            capacity = 100;
        }
 

        [When(@"I schedule a meeting")]
        public void WhenIScheduleAMeeting()
        {
            //var scheduleMeetingCommand = new ScheduleMeetingCommand(MEETING_ID, meetingDate, locationId, speakerId, capacity);

            //new DomainDatabaseBootStrapper().ReCreateDatabaseSchema();

            //var sqliteConnectionString = string.Format("Data Source={0}", DATA_BASE_FILE);

            //handler = new ScheduleMeetingCommandHandler(repository);

            //bus.Publish(scheduleMeetingCommand);

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
