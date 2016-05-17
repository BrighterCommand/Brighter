using System;
using paramore.brighter.commandprocessor;
using SimpleInjector;

namespace ProtoBufSender.Configuration
{
    static class MessageHandlerConfig
    {
        public static HandlerConfiguration Register(Container container)
        {
            //We aren't using any handlers in this very minimal example. If we were, we would configure them here

            //Tell the container how to create message handlers
            var simpleInjectorHandlerFactory = new SimpleInjectorMessageHandlerFactory(container);
            //none needed in this example

            //Associate messages with message handlers
            var subscriberRegistry = new SubscriberRegistry();
            //none needed in this example

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
