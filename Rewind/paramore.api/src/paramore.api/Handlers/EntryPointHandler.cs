using paramore.api.Resources;

namespace paramore.api.Handlers
{
    public class EntryPointHandler
    {
        public EntryPoint Get()
        {
            return new EntryPoint { Title = "Welcome Home." };
        }
    }
}