using Nancy;

namespace paramore.commandprocessor
{
    public class RequestContext : IRequestContext
    {
        public RequestContext()
        {
            Bag = new DynamicDictionary();
        }

        public dynamic Bag { get; private set; }
    }
}