using System;
using Paramore.Brighter;
using TinyIoc;

namespace GenericListener.Infrastructure
{
    public class MessageMapperFactory : IAmAMessageMapperFactory, IAmAMessageMapperRegistry
    {
        readonly TinyIoCContainer _container;

        public MessageMapperFactory(TinyIoCContainer windsorContainer)
        {
            _container = windsorContainer;
        }

        public IAmAMessageMapper Create(Type messageMapperType)
        {
            return (IAmAMessageMapper)_container.Resolve(messageMapperType);
        }

        public IAmAMessageMapper<T> Get<T>() where T : class, IRequest
        {
            return _container.Resolve<IAmAMessageMapper<T>>();
        }

        public void Register<TRequest, TMessageMapper>()
            where TRequest : class, IRequest
            where TMessageMapper : class, IAmAMessageMapper<TRequest>
        {
            _container.Register<IAmAMessageMapper<TRequest>, TMessageMapper>().AsMultiInstance();
        }

        public void Register(Type requestType, Type messageMapper)
        {
            _container.Register(typeof(IAmAMessageMapper<>).MakeGenericType(requestType), messageMapper);
        }
    }
}