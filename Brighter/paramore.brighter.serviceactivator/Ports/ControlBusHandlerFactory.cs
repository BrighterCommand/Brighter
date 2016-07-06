using System;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator.Ports.Handlers;

namespace paramore.brighter.serviceactivator.Ports
{
    internal class ControlBusHandlerFactory : IAmAHandlerFactory
    {
        private readonly Func<IAmACommandProcessor> _commandProcessorFactory;
        private readonly IDispatcher _worker;
        private readonly ILog _logger;

        public ControlBusHandlerFactory(IDispatcher worker, Func<IAmACommandProcessor> commandProcessorFactory) 
            : this(worker, LogProvider.For<ControlBusHandlerFactory>(), commandProcessorFactory)
        {
        }

        public ControlBusHandlerFactory(IDispatcher worker, ILog logger, Func<IAmACommandProcessor> commandProcessorFactory)
        {
            _worker = worker;
            _logger = logger;
            _commandProcessorFactory = commandProcessorFactory;
        }

        /// <summary>
        /// Creates the specified handler type.
        /// </summary>
        /// <param name="handlerType">Type of the handler.</param>
        /// <returns>IHandleRequests.</returns>
        public IHandleRequests Create(Type handlerType)
        {
            if (handlerType == typeof(ConfigurationCommandHandler))
            {
                return new  ConfigurationCommandHandler(_worker, _logger);               
            }
            else if (handlerType == typeof (HeartbeatRequestCommandHandler))
            {
                return new HeartbeatRequestCommandHandler(_commandProcessorFactory(), _worker);
            }

            throw new ArgumentOutOfRangeException("handlerType");
        }

        /// <summary>
        /// Releases the specified handler.
        /// </summary>
        /// <param name="handler">The handler.</param>
        public void Release(IHandleRequests handler)
        {
        }

    }
}
