
using System.Collections.Generic;

namespace paramore.brighter.commandprocessor
{
    public class RequestContext : IRequestContext
    {
        public IAdaptAnInversionOfControlContainer Container { get; set; }
        public Dictionary<string, object> Bag { get; private set; }

        public RequestContext(IAdaptAnInversionOfControlContainer container)
        {
            Container = container;
            Bag = new Dictionary<string, object>();
        }

    }
}