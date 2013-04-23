using System.Data;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Venues;
using Paramore.Ports.Services.Commands.Venue;
using paramore.commandprocessor;

namespace Paramore.Ports.Services.Handlers.Venues
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
            using (var uow = unitOfWorkFactory.CreateUnitOfWork())
            {
                var venue = repository[command.Id];
                repository.Delete(venue);

                uow.Commit();   
            }
            
            return base.Handle(command);
        }
    }
}
