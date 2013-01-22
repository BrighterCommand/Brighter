using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Meetings;
using Paramore.Domain.Venues;
using Paramore.Ports.Services.Commands.Meeting;
using paramore.commandprocessor;

namespace Paramore.Ports.Services.Handlers.Meetings
{
    public class ScheduleMeetingCommandHandler : RequestHandler<ScheduleMeetingCommand>
    {
        private readonly IRepository<Meeting, MeetingDocument> repository;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;
        private readonly IScheduler scheduler;

        public ScheduleMeetingCommandHandler(IScheduler scheduler,IRepository<Meeting, MeetingDocument> repository, IAmAUnitOfWorkFactory unitOfWorkFactory)
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
