using Fohjin.DDD.EventStore;
using UserGroupManagement.Commands;
using UserGroupManagement.Domain.Meetings;

namespace UserGroupManagement.CommandHandlers
{
    public class ScheduleMeetingCommandHandler : IHandleCommands<ScheduleMeetingCommand>
    {
        private readonly IDomainRepository<IDomainEvent> repository;

        public ScheduleMeetingCommandHandler(IDomainRepository<IDomainEvent> repository)
        {
            this.repository = repository;
        }

        public void Handle(ScheduleMeetingCommand command)
        {
            var meeting = new MeetingFactory().Schedule(command.MeetingId, command.On, command.LocationId, command.SpeakerId, command.Capacity);

            repository.Add(meeting);

            //events from meeting?
            //AddNewSessionToSpeakerProfile(meeting.Id);
            //AddLocationUsage(meeting.Id)

        }
    }
}
