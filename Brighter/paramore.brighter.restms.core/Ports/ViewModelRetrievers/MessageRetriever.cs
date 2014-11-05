using System;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.core.Ports.ViewModelRetrievers
{
    public class MessageRetriever
    {
        readonly IAmARepository<Pipe> pipeRepository;
        
        public MessageRetriever(IAmARepository<Pipe> pipeRepository)
        {
            this.pipeRepository = pipeRepository;
        }

        public RestMSMessage Retrieve(Name pipeName, Guid messageId)
        {
            var pipe = pipeRepository[new Identity(pipeName.Value)];
            if (pipe == null)
            {
                throw new PipeDoesNotExistException(string.Format("The message {0} does not exist", pipeName.Value));
            }

            var message = pipe.FindMessage(messageId);

            if (message == null)
            {
                throw new MessageDoesNotExistException(string.Format("Cannot find message {0} on pipe {1}", messageId, pipeName));
            }


            return new RestMSMessage(message);
        }
    }
}
