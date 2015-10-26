using System;
using System.Linq;
using GenericListener.Adapters.Containers;
using GenericListener.Adapters.MessageMappers;
using GenericListener.Adapters.MessageMappers.Tasks;
using GenericListener.Infrastructure;
using GenericListener.Ports.Attributes;
using GenericListener.Ports.Events;
using GenericListener.Ports.Handlers;
using GenericListener.Ports.Handlers.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.serviceactivator;
using Polly;
using Tasks.Ports.Events;
using TinyIoC;
using Topshelf;

namespace GenericListener.Adapters.Services
{
    public class GenericListenerService : ServiceControl
    {
        private static readonly Type[] AppServicesTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).ToArray();
        private static readonly Type[] EventTypes = AppServicesTypes.Where(t => !string.IsNullOrWhiteSpace(t.Namespace) && t.Namespace.Contains(".Events")).ToArray();
        private static readonly Type[] HandlerTypes = AppServicesTypes.Where(t => !string.IsNullOrWhiteSpace(t.Namespace) && t.Namespace.Contains(".Handlers")).ToArray();
        private static readonly Type[] MapperTypes = typeof(GenericMapper<>).Assembly.GetTypes().Where(t => !string.IsNullOrWhiteSpace(t.Namespace) && t.Namespace.Contains(".MessageMappers")).ToArray();

        readonly TinyIoCContainer _container;
        Dispatcher _dispatcher;

        public GenericListenerService()
        {
            _container = new TinyIoCContainerConfiguration().Build();
        }

        public bool Start(HostControl hostControl)
        {
            log4net.Config.XmlConfigurator.Configure();

            var handlers = new HandlerFactory(_container);
            var mappers = new MessageMapperFactory(_container);
            var config = new HandlerConfig(mappers, handlers);
            
            RegisterHandlers(config);

            _dispatcher = BuildDispatcher(config);

            _container.Register<IAmACommandProcessor>(_dispatcher.CommandProcessor);

            _dispatcher.Receive();

            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            _dispatcher.End().Wait();
            _dispatcher = null;

            return true;
        }

        private void RegisterHandlers(HandlerConfig config)
        {
            // Register traditional Brighter event type with appropriate handler and mapper.
            config.Register<TaskReminderSentEvent, TaskReminderSentEventHandler, TaskReminderSentEventMapper>();

            // Register events deriving from GenericTask that optonally confirm to our generic usage
            // hoisting custom version for derived types where found.
            RegisterGenericHandlersFor<GenericTask>(config);

            _container.Register(typeof(LoggingContextHandler<>)).AsMultiInstance();
        }

        private void RegisterGenericHandlersFor<TRequestBase>(HandlerConfig config)
        {
            var baseType = typeof(TRequestBase);

            foreach (var derivedType in EventTypes.Where(t => t.FullName != baseType.FullName && baseType.IsAssignableFrom(t)))
            {
                var handlerImplType = HandlerTypes.FirstOrDefault(t => typeof(IHandleRequests<>).MakeGenericType(derivedType).IsAssignableFrom(t));

                if (handlerImplType == null)
                {
                    var handlerBaseImplType = HandlerTypes.FirstOrDefault(t => t.Name.Equals(baseType.Name + "GenericHandler`1"));

                    if (handlerBaseImplType != null)
                    {
                        handlerImplType = handlerBaseImplType.MakeGenericType(derivedType);
                    }
                }

                var mapperImplType = MapperTypes.FirstOrDefault(t => typeof(IAmAMessageMapper<>).MakeGenericType(derivedType).IsAssignableFrom(t));

                if (mapperImplType == null)
                { 
                   var mapperBaseImplType = MapperTypes.FirstOrDefault(t => t.Name.Equals(baseType.Name + "GenericMapper`1"));

                   if (mapperBaseImplType != null)
                   {
                       mapperImplType = mapperBaseImplType.MakeGenericType(derivedType);
                   }
                }

                config.Register(
                    type: derivedType,
                    handler: handlerImplType ?? typeof(GenericHandler<>).MakeGenericType(derivedType),
                    mapper: mapperImplType ?? typeof(GenericMapper<>).MakeGenericType(derivedType));
            }            
        }

        Dispatcher BuildDispatcher(HandlerConfig handlers)
        {
            var policy = BuildPolicy();
            var logger = LogProvider.GetLogger("Brighter");

            return DispatchBuilder.With()
                .CommandProcessor(CommandProcessorBuilder.With()
                    .Handlers(handlers.Handlers)
                    .Policies(policy)
                    .NoTaskQueues()
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build())
                .MessageMappers(handlers.Mappers)
                .ChannelFactory(new InputChannelFactory(new RmqMessageConsumerFactory(logger), new RmqMessageProducerFactory(logger)))
                .ConnectionsFromConfiguration()
                .Build();
        }

        PolicyRegistry BuildPolicy()
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                {
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(150)
                });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

            return new PolicyRegistry
            {
                {CommandProcessor.RETRYPOLICY, retryPolicy},
                {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
            };
        }

        public void Shutdown(HostControl hostcontrol)
        {
            if (null != _dispatcher)
                Stop(hostcontrol);
        }
    }
}