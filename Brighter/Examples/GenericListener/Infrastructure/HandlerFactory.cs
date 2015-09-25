using System;
using System.Collections.Generic;
using paramore.brighter.commandprocessor;
using TinyIoC;

namespace GenericListener.Infrastructure
{
    public class HandlerFactory : IAmAHandlerFactory, IAmASubscriberRegistry
    {
        private readonly TinyIoCContainer _container;
        private readonly SubscriberRegistry _registry;

        public HandlerFactory(TinyIoCContainer container)
        {
            _container = container;
            _registry = new SubscriberRegistry();
        }

        public IHandleRequests Create(Type handlerType)
        {
            return (IHandleRequests)_container.Resolve(handlerType);
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

        public IEnumerable<Type> Get<T>() where T : class, IRequest
        {
            return _registry.Get<T>();
        }

        public void Register<TRequest, TImplementation>()
            where TRequest : class, IRequest
            where TImplementation : class, IHandleRequests<TRequest>
        {
            _container.Register<TImplementation>().AsMultiInstance();
            _registry.Register<TRequest, TImplementation>();
        }

        public void Register(Type requestType, Type messageHandler)
        {
            _container.Register(messageHandler, messageHandler);
            _registry.Add(requestType, messageHandler);
        }
    }
}