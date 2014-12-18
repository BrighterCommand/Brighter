#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using Common.Logging;
using Microsoft.Practices.Unity;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Handlers;
using paramore.brighter.restms.core.Ports.Repositories;
using paramore.brighter.restms.server.Adapters.Configuration;
using paramore.brighter.restms.server.Adapters.Controllers;
using paramore.brighter.restms.server.Adapters.Security;
using Polly;

namespace paramore.brighter.restms.server.Adapters.Service
{
    internal static class IoCConfiguration
    {
        public static void Run(UnityContainer container)
        {
            container.RegisterType<DomainController>();
            container.RegisterType<FeedController>();
            container.RegisterType<JoinController>();
            container.RegisterType<MessageController>();
            container.RegisterType<PipeController>();
            container.RegisterType<IAmARepository<Domain>, InMemoryDomainRepository>(new ContainerControlledLifetimeManager());
            container.RegisterType<IAmARepository<Feed>, InMemoryFeedRepository>(new ContainerControlledLifetimeManager());
            container.RegisterType<IAmARepository<Pipe>, InMemoryPipeRepository>(new ContainerControlledLifetimeManager());
            container.RegisterType<IAmARepository<Join>, InMemoryJoinRepository>(new ContainerControlledLifetimeManager());
            container.RegisterType<IAmACredentialStore, CredentialStore>(new ContainerControlledLifetimeManager());
            container.RegisterInstance(typeof (ILog), LogManager.GetCurrentClassLogger(), new ContainerControlledLifetimeManager());
            container.RegisterType<AddFeedCommandHandler>();
            container.RegisterType<AddFeedToDomainCommandHandler>();
            container.RegisterType<AddJoinCommandHandler>();
            container.RegisterType<AddJoinToFeedCommandHandler>();
            container.RegisterType<AddJoinToPipeCommandHandler>();
            container.RegisterType<AddMessageToFeedCommandHandler>();
            container.RegisterType<AddPipeCommandHandler>();
            container.RegisterType<AddPipeToDomainCommandHandler>();
            container.RegisterType<DeleteFeedCommandHandler>();
            container.RegisterType<DeleteMessageCommandHandler>();
            container.RegisterType<DeletePipeCommandHandler>();
            container.RegisterType<RemoveFeedFromDomainCommandHandler>();

            var handlerFactory = new UnityHandlerFactory(container);

            var subscriberRegistry = new SubscriberRegistry
            {
                {typeof(AddFeedCommand), typeof(AddFeedCommandHandler)},
                {typeof(AddFeedToDomainCommand), typeof(AddFeedToDomainCommandHandler)},
                {typeof(AddJoinCommand), typeof(AddJoinCommandHandler)},
                {typeof(AddJoinToFeedCommand), typeof(AddJoinToFeedCommandHandler)},
                {typeof(AddJoinToPipeCommand), typeof(AddJoinToPipeCommandHandler)},
                {typeof(AddMessageToFeedCommand), typeof(AddMessageToFeedCommandHandler)},
                {typeof(AddPipeCommand), typeof(AddPipeCommandHandler)},
                {typeof(AddPipeToDomainCommand), typeof(AddPipeToDomainCommandHandler)},
                {typeof(DeleteFeedCommand), typeof(DeleteFeedCommandHandler)},
                {typeof(DeleteMessageCommand), typeof(DeleteMessageCommandHandler)},
                {typeof(DeletePipeCommand), typeof(DeletePipeCommandHandler)},
                {typeof(RemoveFeedFromDomainCommand), typeof(RemoveFeedFromDomainCommandHandler)}
            };

            //create policies
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

            var policyRegistry = new PolicyRegistry()
            {
                {CommandProcessor.RETRYPOLICY, retryPolicy},
                {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
            };


            var commandProcessor = CommandProcessorBuilder.With()
                    .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                    .Policies(policyRegistry)
                    .Logger(container.Resolve<ILog>())
                    .NoTaskQueues()
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build();

            container.RegisterInstance(typeof (IAmACommandProcessor), commandProcessor);
        }
    }
}