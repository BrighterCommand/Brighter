using System;
using GenericListener.Infrastructure;
using paramore.brighter.commandprocessor;

namespace GenericListener.Adapters.Services
{
    public class HandlerConfig
    {
        private readonly MessageMapperFactory _mappers;
        private readonly HandlerFactory _handlers;

        public HandlerConfig(MessageMapperFactory mappers, HandlerFactory handlers)
        {
            _mappers = mappers;
            _handlers = handlers;
        }

        public void Register<TRequest, THandler, TMapper>() 
            where TRequest : class, IRequest 
            where TMapper : class, IAmAMessageMapper<TRequest> 
            where THandler : class, IHandleRequests<TRequest>
        {
            _mappers.Register<TRequest, TMapper>();
            _handlers.Register<TRequest, THandler>();
        }

        public void Register(Type type, Type handler, Type mapper)
        {
            _mappers.Register(type, mapper);
            _handlers.Register(type, handler);
        }

        public void Register<TRequest, THandler>()
            where TRequest : class, IRequest
            where THandler : class, IHandleRequests<TRequest>
        {
            _handlers.Register<TRequest, THandler>();
        }

        public IAmAMessageMapperRegistry Mappers
        {
            get { return _mappers; }
        }

        public HandlerConfiguration Handlers
        {
            get { return new HandlerConfiguration(_handlers, _handlers); }
        }
    }
}
