using System;
using System.Collections.Generic;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Venues;
using Raven.Client.Linq;

namespace Paramore.Rewind.Core.Ports.ThinReadLayer
{
    public class VenueReader : IAmAViewModelReader<VenueDocument>
    {
        private readonly bool allowStale;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;

        public VenueReader(IAmAUnitOfWorkFactory unitOfWorkFactory, bool allowStale = false)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.allowStale = allowStale;
        }

        public IEnumerable<VenueDocument> GetAll()
        {
            IRavenQueryable<VenueDocument> venues;
            using (IUnitOfWork unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
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
            using (IUnitOfWork unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                return unitOfWork.Load<VenueDocument>(id);
            }
        }
    }
}