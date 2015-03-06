using paramore.commandprocessor;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Venues;
using Paramore.Rewind.Core.Ports.Commands.Venue;

namespace Paramore.Rewind.Core.Ports.Handlers.Venues
{
    public class AddVenueCommandHandler : RequestHandler<AddVenueCommand>
    {
        private readonly IRepository<Venue, VenueDocument> repository;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;

        public AddVenueCommandHandler(IRepository<Venue, VenueDocument> repository, IAmAUnitOfWorkFactory unitOfWorkFactory)
        {
            this.repository = repository;
            this.unitOfWorkFactory = unitOfWorkFactory;
        }

        public override AddVenueCommand Handle(AddVenueCommand command)
        {
            var venue = new Venue(
                version: new Version(),
                name: command.VenueName,
                address: command.Address,
                map: command.VenueMap,
                contact: command.Contact);

            using (IUnitOfWork unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Add(venue);
                unitOfWork.Commit();
            }

            command.Id = venue.Id;

            return base.Handle(command);
        }
    }
}