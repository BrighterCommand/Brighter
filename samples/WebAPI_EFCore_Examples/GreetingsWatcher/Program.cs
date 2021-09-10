using System;
using System.Threading.Tasks;
using GreetingsPorts.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

namespace GreetingsWatcher
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var subscriptions = new Subscription[]
                    {
                        new RmqSubscription<GreetingMade>(
                            new SubscriptionName("paramore.sample.greetingswatcher"),
                            new ChannelName("greetingsWatcher"),
                            new RoutingKey("GreetingMade"),
                            timeoutInMilliseconds: 200,
                            isDurable: true,
                            highAvailability: true,
                            makeChannels: OnMissingChannel.Create), //change to OnMissingChannel.Validate if you have infrastructure declared elsewhere
                    };

                    var rmqConnection = new RmqMessagingGatewayConnection
                    {
                        AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                        Exchange = new Exchange("paramore.brighter.exchange")
                    };

                    var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);

                    services.AddServiceActivator(options =>
                        {
                            options.Subscriptions = subscriptions;
                            options.ChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
                        })
                        .UseInMemoryOutbox()
                        .UseExternalBus(new RmqMessageProducer(rmqConnection))
                        .AutoFromAssemblies();

                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .UseConsoleLifetime();
    }
}
