using System;

namespace Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration
{
    public class RmqGatewayBuilder : 
        RmqGatewayBuilder.IRmqGatewayBuilderUri, 
        RmqGatewayBuilder.IRmqGatewayBuilderExchange, 
        RmqGatewayBuilder.IRmqGatewayBuilderQueues
    {
        private string _exchangeName;
        private Uri _ampqUri;

        private RmqGatewayBuilder() {  }

        public static IRmqGatewayBuilderUri With => new RmqGatewayBuilder();

        public IRmqGatewayBuilderExchange Uri(Uri uri)
        {
            _ampqUri = uri;
            return this;
        }

        public IRmqGatewayBuilderQueues Exchange(string exchangeName)
        {
            _exchangeName = exchangeName;
            return this;
        }

        public RmqMessagingGatewayConnection  DefaultQueues()
        {
            return new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(_ampqUri),
                Exchange = new Exchange(_exchangeName)
            };
        }

        public interface IRmqGatewayBuilderUri
        {
            IRmqGatewayBuilderExchange Uri(Uri uri);
        }

        public interface IRmqGatewayBuilderExchange
        {
            IRmqGatewayBuilderQueues Exchange(string exchangeName);
        }

        public interface IRmqGatewayBuilderQueues
        {
            RmqMessagingGatewayConnection DefaultQueues();
        }
    }
}