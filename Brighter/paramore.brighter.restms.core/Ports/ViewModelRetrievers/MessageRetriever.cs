using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.core.Ports.ViewModelRetrievers
{
    public class MessageRetriever
    {
        readonly IAmARepository<Message> messageRepository;

        public MessageRetriever(IAmARepository<Message> messageRepository)
        {
            this.messageRepository = messageRepository;
        }

        public RestMSMessage Retrieve(Name messageName)
        {
            var message = messageRepository[new Identity(messageName.Value)];
            if (message == null)
            {
                throw new MessageDoesNotExistException(string.Format("The message {0} does not exist", messageName.Value));
            }

            return new RestMSMessage(message);
        }
    }
}
