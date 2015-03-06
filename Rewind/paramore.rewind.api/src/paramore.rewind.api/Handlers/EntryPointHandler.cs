using OpenRasta.Web;
using paramore.rewind.adapters.presentation.api.Resources;

namespace paramore.rewind.adapters.presentation.api.Handlers
{
    public class EntryPointHandler
    {
        public OperationResult Get()
        {
            return new OperationResult.OK { ResponseResource = new EntryPointResource { Title = "ToDo: Add Resource Index" } };
        }
    }
}