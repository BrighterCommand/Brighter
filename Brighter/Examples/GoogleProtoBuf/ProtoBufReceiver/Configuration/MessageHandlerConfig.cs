using System;
using Brighter.Example.Messages;
using ProtoBufReceiver.MessageHandlers;
using paramore.brighter.commandprocessor;
using SimpleInjector;

namespace ProtoBufReceiver.Configuration
{
    static class MessageHandlerConfig
    {
        public static HandlerConfiguration Register(Container container)
        {
            var simpleInjectorHandlerFactory = new SimpleInjectorMessageHandlerFactory(container);
            var subscriberRegistry = new SubscriberRegistry();

            //Tell the container how to create message handlers
            container.Register<IHandleRequests<SampleCommand>, SampleCommandHandler>();
            container.Register<IHandleRequests<SampleEvent>, SampleEventHandler>();

            //Associate messages with message handlers
            subscriberRegistry.Register<SampleCommand, SampleCommandHandler>();
            subscriberRegistry.Register<SampleEvent, SampleEventHandler>();

            return new HandlerConfiguration(subscriberRegistry, simpleInjectorHandlerFactory);
        }
    }

    internal class SimpleInjectorMessageHandlerFactory : IAmAHandlerFactory
    {
        readonly Container _container;

        public SimpleInjectorMessageHandlerFactory(Container container)
        {
            _container = container;
        }

        public IHandleRequests Create(Type handlerType)
        {
            return (IHandleRequests)_container.GetInstance(handlerType);
        }

        public void Release(IHandleRequests handler)
        {
            var disposable = handler as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
            handler = null;
        }
    }
}
