using System;
using Brighter.Example.Messages;
using paramore.brighter.commandprocessor;
using SimpleInjector;

namespace ProtoBufReceiver.Configuration
{
    static class MessageMapperConfig
    {
        public static MessageMapperRegistry Register(Container container)
        {
            //Tell the container how to create message mappers
            var simpleInjectorMessageMapperFactory = new SimpleInjectorMessageMapperFactory(container);

            //Associate messages with message mappers
            var messageMapperRegistry = new MessageMapperRegistry(simpleInjectorMessageMapperFactory);
            messageMapperRegistry.Register<SampleCommand, SampleCommandBrighterMapper>();
            messageMapperRegistry.Register<SampleEvent, SampleEventBrighterMapper>();

            return messageMapperRegistry;
        }
    }

    class SimpleInjectorMessageMapperFactory : IAmAMessageMapperFactory
    {
        private readonly Container _container;

        public SimpleInjectorMessageMapperFactory(Container container)
        {
            _container = container;
        }

        public IAmAMessageMapper Create(Type messageMapperType)
        {
            return (IAmAMessageMapper)_container.GetInstance(messageMapperType);
        }
    }
}
