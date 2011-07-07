using System;
using System.Runtime.Serialization.Formatters.Binary;
using Fohjin.DDD.Bus;
using Fohjin.DDD.Configuration;
using Fohjin.DDD.EventStore;
using Fohjin.DDD.EventStore.SQLite;
using Fohjin.DDD.EventStore.Storage;
using NUnit.Framework;
using Rhino.Mocks;
using SpecUnit;
using UserGroupManagement.CommandHandlers;
using UserGroupManagement.Commands;
using UserGroupManagement.Domain.Meetings;

namespace UserGroupManagement.Tests.CommandHandlers.ConcerningScheduleMeetingCommandHandler
{
    [Concern(typeof(ScheduleMeetingCommandHandler))]
    [TestFixture]
    public class WhenSchedulingANewMeeting : ContextSpecification
    {
        private ScheduleMeetingCommandHandler scheduleMeetingCommandHandler;
        private ScheduleMeetingCommand scheduleMeetingCommand;
        private DomainRepository<IDomainEvent> repository;
        private readonly Guid meetingId = Guid.NewGuid();
        private Meeting meeting;
        private readonly DateTime meetingTime = DateTime.Now;
        private readonly Guid locationId = Guid.NewGuid();
        private readonly Guid speakerId = Guid.NewGuid();
        private const int CAPACITY = 100;
        private const string DATA_BASE_FILE = "domainDataBase.db3";

        protected override void Context()
        {
            new DomainDatabaseBootStrapper().ReCreateDatabaseSchema();

            var sqliteConnectionString = string.Format("Data Source={0}", DATA_BASE_FILE);

            var domainEventStorage = new DomainEventStorage<IDomainEvent>(sqliteConnectionString, new BinaryFormatter());
            var eventStoreIdentityMap = new EventStoreIdentityMap<IDomainEvent>();
            var eventStoreUnitOfWork = new EventStoreUnitOfWork<IDomainEvent>(domainEventStorage, eventStoreIdentityMap, MockRepository.GenerateStub<IBus>());
            repository = new DomainRepository<IDomainEvent>(eventStoreUnitOfWork, eventStoreIdentityMap);
            
            scheduleMeetingCommand = new ScheduleMeetingCommand(meetingId, meetingTime, locationId, speakerId, CAPACITY);

            scheduleMeetingCommandHandler = new ScheduleMeetingCommandHandler(repository);
        }

        protected override void Because()
        {
            scheduleMeetingCommandHandler.Handle(scheduleMeetingCommand);
            meeting = repository.GetById<Meeting>(meetingId);
        }

        [Test]
        public void ShouldMatchMeeting()
        {
            meeting.ShouldEqual(new Meeting(meetingId, meetingTime, locationId, speakerId, CAPACITY));
        }
    }
}
