using System.Linq;
using OpenRasta.Web;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Ports.Services.ThinReadLayer;

namespace Paramore.Adapters.Presentation.API.Handlers
{
    public class VenueEndPointHandler
    {
        private readonly IAmAUnitOfWorkFactory _unitOfWorkFactory;

        public VenueEndPointHandler(IAmAUnitOfWorkFactory unitOfWorkFactory)
        {
            _unitOfWorkFactory = unitOfWorkFactory;
        }

        public OperationResult Get()
        {
            var venues = new VenueReader(_unitOfWorkFactory, false).GetAll().ToList();

            return new OperationResult.OK 
            { 
                ResponseResource = venues
            };
        }
    }
}
