using System;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration.ConfigurationSections;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.Handlers
{
    public class MessageProducerFactoryProvider : IMessageProducerFactoryProvider
    {
        public IAmAMessageProducerFactory Get(ILog logger)
        {
            return LoadProducer(logger);
        }

        private IAmAMessageProducerFactory LoadProducer(ILog logger)
        {
            var configSection = MessageViewerSection.GetViewerSection;
            var brokerSection = configSection.Broker;

            //ToDo: assume always needs a logger!!!
            //ToDo: factory or actual producer???

            var orType = Type.GetType(brokerSection.AssemblyQualifiedName);
            if (orType == null)
            {
                throw new SystemException("Cannot find ProducerFactory " + brokerSection.AssemblyQualifiedName);
            }
            
            var factory = Activator.CreateInstance(orType, logger);
            return factory as IAmAMessageProducerFactory;
        }
    }
}