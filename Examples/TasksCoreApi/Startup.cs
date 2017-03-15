using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Messagestore.Sqlite;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration;
using Polly;
using Tasks.Adapters.DataAccess;
using Tasks.Ports;
using Tasks.Ports.Commands;
using Tasks.Ports.Events;
using Tasks.Ports.Handlers;
using TasksApi.MessageMappers;
using TasksApi.ViewModelRetrievers;
using StructureMap;

namespace TasksApi
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
           // Add framework serviceProvider.
            services.AddMvc();

            services.AddSingleton<ITaskListRetriever, TaskListRetriever>();
            services.AddSingleton<ITaskRetriever, TaskRetriever>();
            services.AddSingleton<ITasksDAO, TasksDAO>();

            //create handler 

            var subscriberRegistry = new SubscriberRegistry();
            services.AddTransient<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            subscriberRegistry.Register<AddTaskCommand, AddTaskCommandHandler>();

            //complete handler 
            services.AddTransient<IHandleRequests<CompleteTaskCommand>, CompleteTaskCommandHandler>();
            subscriberRegistry.Register<CompleteTaskCommand, CompleteTaskCommandHandler>();

            //create policies
            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var policyRegistry = new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } };

            //create message mappers
            services.AddTransient<IAmAMessageMapper<TaskReminderCommand>, TaskReminderCommandMessageMapper>();
            
            var messagingGatewayConfiguration = RmqGatewayBuilder.With.Uri(new Uri(Configuration["RabbitMQ:Uri"])).Exchange(Configuration["RabbitMQ:Exchange"]).DefaultQueues();

            var gateway = new RmqMessageProducer(messagingGatewayConfiguration);
            IAmAMessageStore<Message> sqlMessageStore = new SqliteMessageStore(new SqliteMessageStoreConfiguration("Data Source = tasks.db", "MessageStores"));

            var container = new Container();
            container.Configure(config =>
            {
                var servicesMessageMapperFactory = new ServicesMessageMapperFactory(container);

                var messageMapperRegistry = new MessageMapperRegistry(servicesMessageMapperFactory)
                    {
                        {typeof(TaskReminderCommand), typeof(TaskReminderCommandMessageMapper)},
                        {typeof(TaskAddedEvent), typeof(TaskAddedEventMapper)},
                        {typeof(TaskEditedEvent), typeof(TaskEditedEventMapper)},
                        {typeof(TaskCompletedEvent), typeof(TaskCompletedEventMapper)},
                        {typeof(TaskReminderSentEvent), typeof(TaskReminderSentEventMapper)}
                    };

                var servicesHandlerFactory = new ServicesHandlerFactory(container);

                var commandProcessor = CommandProcessorBuilder.With()
                    .Handlers(new HandlerConfiguration(subscriberRegistry, servicesHandlerFactory))
                    .Policies(policyRegistry)
                    .TaskQueues(new MessagingConfiguration(sqlMessageStore, gateway, messageMapperRegistry))
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build();

                config.For<IAmACommandProcessor>().Singleton().Use(() => commandProcessor);

            });
            container.Populate(services);

            return container.GetInstance<IServiceProvider>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseMvc();
        }
    }

    public class ServicesMessageMapperFactory : IAmAMessageMapperFactory
    {
        private readonly Container _serviceProvider;

        public ServicesMessageMapperFactory(StructureMap.Container serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IAmAMessageMapper Create(Type messageMapperType)
        {
            return  _serviceProvider.GetInstance(messageMapperType) as IAmAMessageMapper;
        }
    }

    public class ServicesHandlerFactory : IAmAHandlerFactory
    {
        private readonly Container _serviceProvider;

        public ServicesHandlerFactory(Container serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IHandleRequests Create(Type handlerType)
        {
            return _serviceProvider.GetInstance(handlerType) as IHandleRequests;
        }

        public void Release(IHandleRequests handler)
        {
            var disposable = handler as IDisposable;
            disposable?.Dispose();
        }
    }
}
