#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Greetings.Adapters.ServiceHost;
using Greetings.Ports.CommandHandlers;
using Greetings.Ports.Commands;
using Greetings.Ports.Mappers;
using Greetings.TinyIoc;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;
using Serilog;

namespace GreetingsServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
              .MinimumLevel.Debug()
              .WriteTo.Console()
              .CreateLogger();

            var container = new TinyIoCContainer();
            container.Register<IHandleRequests<GreetingRequest>, GreetingRequestHandler>();
            container.Register<IAmAMessageMapper<GreetingRequest>, GreetingRequestMessageMapper>();
            container.Register<IAmAMessageMapper<GreetingReply>, GreetingReplyMessageMapper>();

            var handlerFactory = new TinyIocHandlerFactory(container);
            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<GreetingRequest, GreetingRequestHandler>();

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

            var policyRegistry = new PolicyRegistry
            {
              {CommandProcessor.RETRYPOLICY, retryPolicy},
              {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
            };
            
            var messageStore = new InMemoryOutbox();
 
            //create message mappers
            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(GreetingRequest), typeof(GreetingRequestMessageMapper)},
                {typeof(GreetingReply), typeof(GreetingReplyMessageMapper)}
            };

            //create the gateway
            var rmqConnnection = new RmqMessagingGatewayConnection
            {
              AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
              Exchange = new Exchange("paramore.brighter.exchange"),
            };

            var producer = new RmqMessageProducer(rmqConnnection);
            
            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnnection);

            var commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                .Policies(policyRegistry)
                .TaskQueues(new MessagingConfiguration(messageStore, producer, messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();

            container.Register<IAmACommandProcessor>(commandProcessor);
            
            var dispatcher = DispatchBuilder.With()
              .CommandProcessor(commandProcessor)
              .MessageMappers(messageMapperRegistry)
              .DefaultChannelFactory(new ChannelFactory(rmqMessageConsumerFactory))
              .Connections(new Connection[]
              {
                new Connection<GreetingRequest>(
                  new ConnectionName("paramore.example.greeting"),
                  new ChannelName("Greeting.Request"),
                  new RoutingKey("Greeting.Request"),
                  timeoutInMilliseconds: 200,
                  isDurable: true,
                  highAvailability: true)
              }).Build();

            dispatcher.Receive();

            Console.WriteLine("Press Enter to stop ...");
            Console.ReadLine();

            dispatcher.End().Wait();
        }
    }
}
