using System.Collections.Generic;
using Paramore.Domain.Documents;
using Paramore.Infrastructure.Repositories;
using Raven.Client.Linq;

namespace Paramore.Services.ThinReadLayer
{
    public class VenueReader : IViewModelReader<VenueDocument>
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
                    venues.Customize(x => x.WaitForNonStaleResults());
                }
            }

            return venues;
        }
    }
}