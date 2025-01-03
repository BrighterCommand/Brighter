using System;
using Paramore.Brighter.ServiceActivator.Ports.Handlers;

namespace Paramore.Brighter.ServiceActivator.Ports
{
    internal class ControlBusHandlerFactorySync : IAmAHandlerFactorySync
    {
        private readonly Func<IAmACommandProcessor?> _commandProcessorFactory;
        private readonly IDispatcher _worker;

        public ControlBusHandlerFactorySync(IDispatcher worker, Func<IAmACommandProcessor?> commandProcessorFactory) 
        {
            _worker = worker;
            _commandProcessorFactory = commandProcessorFactory;
        }

        /// <summary>
        /// Creates the specified handler type.
        /// </summary>
        /// <param name="handlerType">Type of the handler.</param>
        /// <param name="lifetime">The Brighter Handler Lifetime</param>
        /// <returns>IHandleRequests.</returns>
        public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
        {
            if (handlerType == typeof(ConfigurationCommandHandler))
                return new ConfigurationCommandHandler(_worker);

            if (handlerType == typeof(HeartbeatRequestCommandHandler))
                return new HeartbeatRequestCommandHandler(_commandProcessorFactory(), _worker);

            throw new ArgumentOutOfRangeException(nameof(handlerType));
        }

        /// <summary>
        /// Releases the specified handler.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="lifetime">The Brighter Handler Lifetime</param>
        public void Release(IHandleRequests handler, IAmALifetime lifetime)
        {
        }
    }
}
