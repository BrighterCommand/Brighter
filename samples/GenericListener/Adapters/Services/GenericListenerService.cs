using System;
using System.Collections.Generic;
using System.Linq;
using GenericListener.Adapters.Containers;
using GenericListener.Adapters.MessageMappers;
using GenericListener.Infrastructure;
using GenericListener.Ports.Attributes;
using GenericListener.Ports.Events;
using GenericListener.Ports.Handlers;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration;
using Paramore.Brighter.ServiceActivator;
using Polly;
using TinyIoc;
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

            _container.Register(_dispatcher.CommandProcessor);

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

            //create the gateway
            var rmqConnnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange"),
            };

            //<!-- Events with mapper and handler overrides -->
            //<add connectionName="Task.ReminderSent" channelName="Task.ReminderSent" routingKey="Task.ReminderSent" dataType="Tasks.Ports.Events.TaskReminderSentEvent" noOfPerformers="1" timeOutInMilliseconds="200" />

            //<!-- Generic Events -->
            //<add connectionName="Task.Added" channelName="Task.Added" routingKey="Task.Added" dataType="GenericListener.Ports.Events.GenericTaskAddedEvent" noOfPerformers="1" timeOutInMilliseconds="200" />
            //<add connectionName="Task.Edited" channelName="Task.Edited" routingKey="Task.Edited" dataType="GenericListener.Ports.Events.GenericTaskEditedEvent" noOfPerformers="1" timeOutInMilliseconds="200" />
            //<add connectionName="Task.Completed" channelName="Task.Completed" routingKey="Task.Completed" dataType="GenericListener.Ports.Events.GenericTaskCompletedEvent" noOfPerformers="1" timeOutInMilliseconds="200" />

            var inputChannelFactory = new InputChannelFactory(new RmqMessageConsumerFactory(rmqConnnection));

            var connections = new List<Connection>
            {
                // Generic Events
                new Connection<GenericTaskAddedEvent>(new ConnectionName("Task.Added"), new ChannelName("Task.Added"), new RoutingKey("Task.Added"), noOfPerformers:1, timeoutInMilliseconds: 200),
                new Connection<GenericTaskEditedEvent>(new ConnectionName("Task.Edited"), new ChannelName("Task.Edited"), new RoutingKey("Task.Edited"), noOfPerformers:1, timeoutInMilliseconds: 200),
                new Connection<GenericTaskCompletedEvent>(new ConnectionName("Task.Completed"), new ChannelName("Task.Completed"),new RoutingKey( "Task.Completed"), noOfPerformers:1, timeoutInMilliseconds: 200),
            };
            
            return DispatchBuilder.With()
                .CommandProcessor(CommandProcessorBuilder.With()
                    .Handlers(handlers.Handlers)
                    .Policies(policy)
                    .NoTaskQueues()
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build())
                .MessageMappers(handlers.Mappers)
                .DefaultChannelFactory(inputChannelFactory)
                .Connections(connections)
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
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            };
        }

        public void Shutdown(HostControl hostcontrol)
        {
            if (_dispatcher != null)
                Stop(hostcontrol);
        }
    }
}