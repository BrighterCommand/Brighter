using Paramore.Adapters.Presentation.API.Resources;

namespace Paramore.Adapters.Presentation.API.Handlers
{
    public class EntryPointHandler
    {
        public OperationResult Get()
        {
            return new OperationResult.OK { ResponseResource = new EntryPoint { Title = "Welcome Home." } };
        }
    }
}