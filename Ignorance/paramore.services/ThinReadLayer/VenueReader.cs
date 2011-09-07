using System.Collections.Generic;
using Paramore.Domain.Venues;
using Paramore.Infrastructure.Raven;

namespace Paramore.Services.ThinReadLayer
{
    public class VenueReader
    {
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;

        public VenueReader(IAmAUnitOfWorkFactory unitOfWorkFactory)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
        }

        public IEnumerable<VenueDTO> GetAll()
        {
            IEnumerable<VenueDTO> venues;
            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                venues = unitOfWork.Query<VenueDTO>();
            }

            return venues;
        }
    }
}