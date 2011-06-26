using System;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Fohjin.DDD.Bus.Direct;
using Fohjin.DDD.Configuration;
using Fohjin.DDD.EventStore;
using Fohjin.DDD.EventStore.SQLite;
using Fohjin.DDD.EventStore.Storage;
using Fohjin.DDD.Reporting.Infrastructure;
using TechTalk.SpecFlow;
using UserGroupManagement.CommandHandlers;
using UserGroupManagement.Commands;
using UserGroupManagement.Configuration;
using UserGroupManagement.Reporting.Dto;

namespace UserGroupManagement.Features.Steps
{
    [Binding]
    public class ScheduleAMeetingSteps
    {
        private Guid speakerId;
        private Guid locationId;
        private DateTime meetingDate;
        private int capacity;
        private static readonly Guid MEETING_ID = Guid.NewGuid();
        private SQLiteReportingRepository reportingRepository;
        private ScheduleMeetingCommandHandler handler;
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
            var scheduleMeetingCommand = new ScheduleMeetingCommand(MEETING_ID, meetingDate, locationId, speakerId, capacity);

            new DomainDatabaseBootStrapper().ReCreateDatabaseSchema();

            var sqliteConnectionString = string.Format("Data Source={0}", DATA_BASE_FILE);

            var domainEventStorage = new DomainEventStorage<IDomainEvent>(sqliteConnectionString, new BinaryFormatter());
            var eventStoreIdentityMap = new EventStoreIdentityMap<IDomainEvent>();
            var bus = new DirectBus(new MessageRouter());
            var eventStoreUnitOfWork = new EventStoreUnitOfWork<IDomainEvent>(domainEventStorage, eventStoreIdentityMap, bus);
            var repository = new DomainRepository<IDomainEvent>(eventStoreUnitOfWork, eventStoreIdentityMap);

            new ReportingDatabaseBootStrapper().ReCreateDatabaseSchema();
            reportingRepository = new SQLiteReportingRepository(sqliteConnectionString, new SqlSelectBuilder(), new SqlInsertBuilder(), new SqlUpdateBuilder(), new SqlDeleteBuilder());

            handler = new ScheduleMeetingCommandHandler(repository);

            var messageRouter = new MessageRouter();
            messageRouter.Register<ScheduleMeetingCommand>(command => handler.Handle(command));

            bus.Publish(scheduleMeetingCommand);

            //how do we publish to report, directly or via command handler. Looks like by using transaction handler we go through unit of work whose commit method fires events to BUS
            //so if we have event and then save they get re-ublished and report canpick up
 
        }

        [Then(@"the new meeting should be open for registration")]
        public void ThenTheNewMeetingShouldBeOpenForRegistration()
        {
            var sut = reportingRepository.GetByExample<MeetingDetailsReport>(new { MeetingTime = meetingDate }).FirstOrDefault();
        }


    }
}
