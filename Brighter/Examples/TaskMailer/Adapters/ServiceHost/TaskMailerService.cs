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
using Common.Logging.Configuration;
using Common.Logging.Simple;
using Polly;
using Raven.Client.Embedded;
using TaskMailer.Ports;
using TinyIoC;
using Topshelf;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;
using paramore.brighter.commandprocessor.messagestore.ravendb;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.serviceactivator;

namespace TaskMailer.Adapters.ServiceHost
{
    internal class TaskMailerService : ServiceControl
    {
        private Dispatcher dispatcher;

        public TaskMailerService()
        {
                //construct the container
                var container = new TinyIoCAdapter(new TinyIoCContainer());

                container.Register<IAmAMessageMapper<TaskReminderCommand>, TaskReminderCommandMessageMapper>();

                var properties = new NameValueCollection();
                properties["showDateTime"] = "true";
                LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);

                var logger = LogManager.GetLogger(typeof (Dispatcher));

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

                var gateway = new RMQMessagingGateway(logger);

                var builder = DispatchBuilder.With()
                             .InversionOfControl(container)
                             .WithLogger(logger)
                             .WithCommandProcessor(CommandProcessorBuilder.With()
                                .InversionOfControl(container)
                                .WithLogger(logger)
                                .WithMessaging(new MessagingConfiguration(
                                                messageStore: new RavenMessageStore(new EmbeddableDocumentStore(), logger),
                                                messagingGateway: gateway,
                                                retryPolicy: retryPolicy,
                                                circuitBreakerPolicy: circuitBreakerPolicy))
                                 .WithRequestContextFactory(new InMemoryRequestContextFactory())
                                .Build()
                                )
                             .WithChannelFactory(new RMQInputChannelfactory(gateway)) 
                             .ConnectionsFromConfiguration();
            dispatcher = builder.Build();


        }

        public bool Start(HostControl hostControl)
        {
            dispatcher.Recieve();
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            dispatcher.End();
            dispatcher = null;
            return false;
        }

        public void Shutdown(HostControl hostcontrol)
        {
            if (dispatcher != null)
                dispatcher.End();
            return;
        }
    }
}