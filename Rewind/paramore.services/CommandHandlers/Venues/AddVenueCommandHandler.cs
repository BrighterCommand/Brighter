using Paramore.Domain.Documents;
using Paramore.Domain.ValueTypes;
using Paramore.Domain.Venues;
using Paramore.Infrastructure.Repositories;
using Paramore.Services.Commands.Venue;
using paramore.commandprocessor;

namespace Paramore.Services.CommandHandlers.Venues
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
            var venue = new Venue(new Id(command.Id), new Version(), new VenueName(command.VenueName));

            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Add(venue);
                unitOfWork.Commit();
            }

            return base.Handle(command);
        }
    }
}