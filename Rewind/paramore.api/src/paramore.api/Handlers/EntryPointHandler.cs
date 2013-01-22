using Paramore.Adapters.Presentation.API.Resources;

namespace Paramore.Adapters.Presentation.API.Handlers
{
    public class EntryPointHandler
    {
        public EntryPoint Get()
        {
            return new EntryPoint { Title = "Welcome Home." };
        }
    }
}