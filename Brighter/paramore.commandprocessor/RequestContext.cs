
using System.Collections.Generic;

namespace paramore.commandprocessor
{
    public class RequestContext : IRequestContext
    {
        public RequestContext()
        {
            Bag = new Dictionary<string, object>();
        }

        public Dictionary<string, object> Bag { get; private set; }
    }
}