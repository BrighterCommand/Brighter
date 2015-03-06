using paramore.commandprocessor;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Meetings;
using Paramore.Rewind.Core.Domain.Venues;
using Paramore.Rewind.Core.Ports.Commands.Meeting;

namespace Paramore.Rewind.Core.Ports.Handlers.Meetings
{
    public class ScheduleMeetingCommandHandler : RequestHandler<ScheduleMeetingCommand>
    {
        private readonly IRepository<Meeting, MeetingDocument> repository;
        private readonly IScheduler scheduler;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;

        public ScheduleMeetingCommandHandler(IScheduler scheduler, IRepository<Meeting, MeetingDocument> repository, IAmAUnitOfWorkFactory unitOfWorkFactory)
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

            using (IUnitOfWork unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Add(meeting);
                unitOfWork.Commit();
            }

            return base.Handle(command);
        }
    }
}