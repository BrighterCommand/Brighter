using System;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator.Ports.Handlers;

namespace paramore.brighter.serviceactivator.Ports
{
    internal class ControlBusHandlerFactory : IAmAHandlerFactory
    {
        private readonly IDispatcher _worker;
        private readonly ILog _logger;

        public ControlBusHandlerFactory(IDispatcher worker, ILog logger)
        {
            _worker = worker;
            _logger = logger;
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
                return new  ConfigurationCommandHandler(_logger, _worker);               
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
