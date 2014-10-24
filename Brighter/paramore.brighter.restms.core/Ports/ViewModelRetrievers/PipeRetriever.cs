using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.core.Ports.ViewModelRetrievers
{
    public class PipeRetriever
    {
        public RestMSPipe Retrieve(Name name)
        {
            return new RestMSPipe();
        }
    }
}
