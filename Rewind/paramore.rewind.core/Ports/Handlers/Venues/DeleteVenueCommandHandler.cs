using paramore.commandprocessor;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Venues;
using Paramore.Rewind.Core.Ports.Commands.Venue;

namespace Paramore.Rewind.Core.Ports.Handlers.Venues
{
    public class DeleteVenueCommandHandler : RequestHandler<DeleteVenueCommand>
    {
        private readonly IRepository<Venue, VenueDocument> repository;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;

        public DeleteVenueCommandHandler(IRepository<Venue, VenueDocument> repository, IAmAUnitOfWorkFactory unitOfWorkFactory)
        {
            this.repository = repository;
            this.unitOfWorkFactory = unitOfWorkFactory;
        }

        public override DeleteVenueCommand Handle(DeleteVenueCommand command)
        {
            using (IUnitOfWork uow = unitOfWorkFactory.CreateUnitOfWork())
            {
                var venue = repository[command.Id];
                repository.Delete(venue);

                uow.Commit();
            }

            return base.Handle(command);
        }
    }
}