using Paramore.Domain.Meetings;
using Paramore.Infrastructure.Domain;
using Paramore.Services.Commands.Meeting;

namespace Paramore.Services.CommandHandlers.Meetings
{
    public class ScheduleMeetingCommandHandler : RequestHandler<ScheduleMeetingCommand>
    {
        private readonly IRepository<Meeting> repository;
        private readonly IScheduler scheduler;

        public ScheduleMeetingCommandHandler(IRepository<Meeting> repository, IScheduler scheduler)
        {
            this.repository = repository;
            this.scheduler = scheduler;
        }

        public override ScheduleMeetingCommand Handle(ScheduleMeetingCommand command)
        {
            var meeting = scheduler
                .Schedule(new Id(command.MeetingId), new MeetingDate(command.On), new Id(command.LocationId), new Id(command.SpeakerId), new Capacity(command.Capacity));

            repository.Add(meeting);

            //events from meeting?
            //AddNewSessionToSpeakerProfile(meeting.Id);
            //AddLocationUsage(meeting.Id)
            return base.Handle(command);
        }
    }
}
