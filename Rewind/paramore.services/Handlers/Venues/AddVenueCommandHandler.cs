using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Venues;
using Paramore.Ports.Services.Commands.Venue;
using paramore.commandprocessor;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Ports.Services.Handlers.Venues
{
    public class AddVenueCommandHandler : RequestHandler<AddVenueCommand>
    {
        private readonly IRepository<Venue, VenueDocument> repository;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;

        public AddVenueCommandHandler(IRepository<Venue,VenueDocument> repository, IAmAUnitOfWorkFactory unitOfWorkFactory)
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

            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
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