using OpenRasta.Web;
using Paramore.Adapters.Presentation.API.Resources;

namespace Paramore.Adapters.Presentation.API.Handlers
{
    public class EntryPointHandler
    {
        public OperationResult Get()
        {
            return new OperationResult.OK { ResponseResource = new EntryPointResource { Title = "ToDo: Add Resource Index" } };
        }
    }
}