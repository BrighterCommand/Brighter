using System;
using System.Collections.Generic;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Venues;
using Raven.Client.Linq;

namespace Paramore.Ports.Services.ThinReadLayer
{
    public class VenueReader : IAmAViewModelReader<VenueDocument>
    {
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;
        private readonly bool allowStale;

        public VenueReader(IAmAUnitOfWorkFactory unitOfWorkFactory, bool allowStale = false)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.allowStale = allowStale;
        }

        public IEnumerable<VenueDocument> GetAll()
        {
            IRavenQueryable<VenueDocument> venues;
            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                venues = unitOfWork.Query<VenueDocument>();
                if (!allowStale)
                {
                    venues.Customize(x => x.WaitForNonStaleResultsAsOfLastWrite());
                }
            }

            return venues;
        }

        public VenueDocument Get(Guid id)
        {
            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                return unitOfWork.Load<VenueDocument>(id);
            }
        }
    }
}