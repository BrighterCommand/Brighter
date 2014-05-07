using paramore.brighter.serviceactivator;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    public class RMQInputChannelfactory : IAmAChannelFactory 
    {
        private readonly RMQMessagingGateway gateway;

        public RMQInputChannelfactory(RMQMessagingGateway gateway)
        {
            this.gateway = gateway;
        }

        public IAmAnInputChannel Create(string channelName)
        {
            return new RMQInputChannel(channelName, gateway);
        }
    }
}
