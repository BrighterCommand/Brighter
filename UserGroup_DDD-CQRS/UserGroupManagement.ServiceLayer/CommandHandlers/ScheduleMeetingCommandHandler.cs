using System;
using UserGroupManagement.Domain.Meetings;
using UserGroupManagement.ServiceLayer.Commands;

namespace UserGroupManagement.ServiceLayer.CommandHandlers
{
    public class ScheduleMeetingCommandHandler : RequestHandler<ScheduleMeetingCommand>
    {
        //private readonly IDomainRepository<IDomainEvent> repository;

        //public ScheduleMeetingCommandHandler(IDomainRepository<IDomainEvent> repository)
        //{
        //    this.repository = repository;
        //}

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
