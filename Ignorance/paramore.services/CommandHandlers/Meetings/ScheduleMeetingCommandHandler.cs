using Paramore.Domain.Meetings;
using Paramore.Infrastructure.Domain;
using Paramore.Services.Commands;
using Paramore.Services.Commands.Meeting;

namespace Paramore.Services.CommandHandlers
{
    public class ScheduleMeetingCommandHandler : RequestHandler<ScheduleMeetingCommand>
    {
        private IRepository<Meeting> repository;

        public ScheduleMeetingCommandHandler(IRepository<Meeting> repository)
        {
            this.repository = repository;
        }

        public override ScheduleMeetingCommand Handle(ScheduleMeetingCommand command)
        {
        //    var meeting = new MeetingFactory().Schedule(command.MeetingId, command.On, command.LocationId, command.SpeakerId, command.Capacity);

        //    repository.Add(meeting);

        //    //events from meeting?
        //    //AddNewSessionToSpeakerProfile(meeting.Id);
        //    //AddLocationUsage(meeting.Id)
            return base.Handle(command);
        }
    }
}
