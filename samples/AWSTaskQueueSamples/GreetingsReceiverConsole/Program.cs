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
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Greetings.Adapters.ServiceHost;
using Greetings.Ports.CommandHandlers;
using Greetings.Ports.Commands;
using Greetings.Ports.Mappers;
using Greetings.TinyIoc;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;
using Serilog;

namespace GreetingsReceiverConsole
{
    public class Program
    {
      public static void Main(string[] args)
      {
        Log.Logger = new LoggerConfiguration()
          .MinimumLevel.Debug()
          .WriteTo.LiterateConsole()
          .CreateLogger();

        var container = new TinyIoCContainer();

        var handlerFactory = new TinyIocHandlerFactory(container);
        var messageMapperFactory = new TinyIoCMessageMapperFactory(container);
        container.Register<IHandleRequests<GreetingEvent>, GreetingEventHandler>();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<GreetingEvent, GreetingEventHandler>();

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

        //create message mappers
        var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
        {
          {typeof(GreetingEvent), typeof(GreetingEventMessageMapper)}
        };

        //create the gateway
      if (new CredentialProfileStoreChain().TryGetAWSCredentials("default", out var credentials))
      {
          var awsConnection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.EUWest1);

          var sqsMessageConsumerFactory = new SqsMessageConsumerFactory(awsConnection);

          var dispatcher = DispatchBuilder.With()
              .CommandProcessor(CommandProcessorBuilder.With()
                  .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                  .Policies(policyRegistry)
                  .NoTaskQueues()
                  .RequestContextFactory(new InMemoryRequestContextFactory())
                  .Build())
              .MessageMappers(messageMapperRegistry)
              .DefaultChannelFactory(new ChannelFactory(awsConnection, sqsMessageConsumerFactory))
              .Connections(new Connection[]
              {
                  new Connection<GreetingEvent>(
                      new ConnectionName("paramore.example.greeting"),
                      new ChannelName(typeof(GreetingEvent).FullName.ToValidSNSTopicName()),
                      new RoutingKey(typeof(GreetingEvent).FullName.ToValidSNSTopicName()),
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
}
