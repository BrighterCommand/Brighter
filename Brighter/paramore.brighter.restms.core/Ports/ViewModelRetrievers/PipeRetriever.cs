using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.core.Ports.ViewModelRetrievers
{
    public class PipeRetriever
    {
        readonly IAmARepository<Pipe> pipeRepository;

        public PipeRetriever(IAmARepository<Pipe> pipeRepository)
        {
            this.pipeRepository = pipeRepository;
        }

        public RestMSPipe Retrieve(Name name)
        {
            var pipe = pipeRepository[new Identity(name.Value)];

            if (pipe == null)
            {
                throw new PipeDoesNotExistException(string.Format("Cannot find pipe named {0}", name.Value));
            }

            return new RestMSPipe(pipe);
        }
    }
}
