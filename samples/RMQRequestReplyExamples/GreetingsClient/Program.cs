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
using Serilog;

namespace GreetingsSender
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
            container.Register<IHandleRequests<GreetingReply>, GreetingReplyHandler>();
            container.Register<IAmAMessageMapper<GreetingReply>, GreetingReplyMessageMapper>();
            container.Register<IAmAMessageMapper<GreetingRequest>, GreetingRequestMessageMapper>();

            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);
            var handerFactory = new TinyIocHandlerFactory(container);

            var subscriberRegistry = new SubscriberRegistry()
            {
                {typeof(GreetingReply), typeof(GreetingReplyHandler)}
            };

            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(GreetingRequest), typeof(GreetingRequestMessageMapper)},
                {typeof(GreetingReply), typeof(GreetingReplyMessageMapper)}
            };

            var rmqConnnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                Exchange = new Exchange("paramore.brighter.exchange"),
            };
            
            var producer = new RmqMessageProducer(rmqConnnection);
            var inputChannelFactory = new ChannelFactory(new RmqMessageConsumerFactory(rmqConnnection));
            
            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(subscriberRegistry, handerFactory))
                .DefaultPolicy()
                .RequestReplyQueues(
                    new MessagingConfiguration(
                        null, 
                        producer, 
                        messageMapperRegistry,
                        responseChannelFactory: inputChannelFactory))
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();

            Console.WriteLine("Requesting Salutation...");
            
            //blocking call
            commandProcessor.Call<GreetingRequest, GreetingReply>(new GreetingRequest{Name = "Ian", Language = "en-gb"}, 2000 );
            
            Console.WriteLine("Done...");
        }
    }
}
