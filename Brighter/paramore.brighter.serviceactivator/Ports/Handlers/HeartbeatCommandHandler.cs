using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.extensions;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator.Ports.Commands;

namespace paramore.brighter.serviceactivator.Ports.Handlers
{
    public class HeartbeatCommandHandler : RequestHandler<HeartbeatCommand>
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IDispatcher _dispatcher;

        public HeartbeatCommandHandler(IAmACommandProcessor commandProcessor, IDispatcher dispatcher)
            : this(LogProvider.GetCurrentClassLogger(), commandProcessor, dispatcher) {}

        public HeartbeatCommandHandler(ILog logger, IAmACommandProcessor commandProcessor, IDispatcher dispatcher)
            : base(logger)
        {
            _commandProcessor = commandProcessor;
            _dispatcher = dispatcher;
        }

        public override HeartbeatCommand Handle(HeartbeatCommand command)
        {
            var heartbeat = new HeartbeatEvent(_dispatcher.HostName);
            _dispatcher.Consumers.Each((consumer) => heartbeat.Consumers.Add(new RunningConsumer(consumer.Name, consumer.State)));

            var topic = Context.Callback.RoutingKey;
            var correlationId = Context.Callback.CorrelationId;

            _commandProcessor.Post(topic, correlationId, heartbeat);

            return base.Handle(command);
        }
    }
}
