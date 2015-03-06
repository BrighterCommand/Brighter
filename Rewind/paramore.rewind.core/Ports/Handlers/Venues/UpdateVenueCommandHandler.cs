using System.Data;
using paramore.commandprocessor;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Venues;
using Paramore.Rewind.Core.Ports.Commands.Venue;

namespace Paramore.Rewind.Core.Ports.Handlers.Venues
{
    public class UpdateVenueCommandHandler : RequestHandler<UpdateVenueCommand>
    {
        private readonly IRepository<Venue, VenueDocument> repository;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;

        public UpdateVenueCommandHandler(IRepository<Venue, VenueDocument> repository, IAmAUnitOfWorkFactory unitOfWorkFactory)
        {
            this.repository = repository;
            this.unitOfWorkFactory = unitOfWorkFactory;
        }

        public override UpdateVenueCommand Handle(UpdateVenueCommand command)
        {
            using (IUnitOfWork uow = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = uow;
                var venue = repository[command.Id];
                if (venue.Version != command.Version)
                {
                    throw new OptimisticConcurrencyException(string.Format("Expected version {0}, but aggregate is at version {1})", command.Version,
                                                                           venue.Version));
                }
                venue.Update(command.VenueName, command.Address, command.Contact, command.VenueMap);
                uow.Commit();
            }
            return base.Handle(command);
        }
    }
}