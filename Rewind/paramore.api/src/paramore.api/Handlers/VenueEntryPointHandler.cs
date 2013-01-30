// ReSharper disable RedundantUsingDirective
using OpenRasta.Web;
// ReSharper restore RedundantUsingDirective
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
            return new OperationResult.OK 
            { 
                ResponseResource = new VenueReader(_unitOfWorkFactory, true).GetAll()
            };
        }
    }
}
