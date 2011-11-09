using Paramore.Domain.Meetings;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Paramore.Services.Commands.Meeting;
using paramore.commandprocessor;

namespace Paramore.Services.CommandHandlers.Meetings
{
    public class ScheduleMeetingCommandHandler : RequestHandler<ScheduleMeetingCommand>
    {
        private readonly IRepository<Meeting, MeetingDTO> repository;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;
        private readonly IScheduler scheduler;

        public ScheduleMeetingCommandHandler(IScheduler scheduler,IRepository<Meeting, MeetingDTO> repository, IAmAUnitOfWorkFactory unitOfWorkFactory)
        {
            this.repository = repository;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.scheduler = scheduler;
        }

        public override ScheduleMeetingCommand Handle(ScheduleMeetingCommand command)
        {
            var meeting = scheduler.Schedule(
                new Id(command.MeetingId), 
                new MeetingDate(command.On), 
                new Id(command.VenueId), 
                new Id(command.SpeakerId), 
                new Capacity(command.Capacity));

            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Add(meeting);
                unitOfWork.Commit();
            }

            return base.Handle(command);
        }
    }
}
