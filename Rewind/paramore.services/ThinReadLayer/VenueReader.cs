using System.Collections.Generic;
using Paramore.Domain.Venues;
using Paramore.Infrastructure.Raven;
using Raven.Client.Linq;

namespace Paramore.Services.ThinReadLayer
{
    public class VenueReader
    {
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;
        private readonly bool allowStale;

        public VenueReader(IAmAUnitOfWorkFactory unitOfWorkFactory, bool allowStale = false)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.allowStale = allowStale;
        }

        public IEnumerable<VenueDTO> GetAll()
        {
            IRavenQueryable<VenueDTO> venues;
            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                venues = unitOfWork.Query<VenueDTO>();
                if (!allowStale)
                {
                    venues.Customize(x => x.WaitForNonStaleResults());
                }
            }

            return venues;
        }
    }
}