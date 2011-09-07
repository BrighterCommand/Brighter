using System.Dynamic;
using Nancy;
using Paramore.Infrastructure.Raven;
using Paramore.Services.ThinReadLayer;

namespace Paramore.Web.Modules
{
    public class VenueModule : NancyModule
    {
        public VenueModule(IAmAUnitOfWorkFactory unitOfWorkFactory)
        {
            Get["venues/index"] = _ =>
            {
                dynamic model = new ExpandoObject();
                model.Venues = new VenueReader(unitOfWorkFactory).GetAll();
                return View["venues/index", model];
            };
        }
    }
}