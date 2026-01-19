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
using System.Collections.Generic;
using Greetings.Ports.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.Configuration;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Serilog;
using Serilog.Extensions.Logging;

namespace GreetingsSender;

static class Program
{
    static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory());

        serviceCollection
            .AddBrighter()
            .AddProducers((configure) =>
            {
                configure.ProducerRegistry = configuration.CreateProducerRegistry<RmqProducerRegistryFactory>("messaging");
                configure.MaxOutStandingMessages = 5;
                configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
            })
            .UsePublicationFinder<CustomPublicationFinder>()
            .AutoFromAssemblies();

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var commandProcessor = serviceProvider.GetRequiredService<IAmACommandProcessor>();

        commandProcessor.Post(new GreetingEvent("Ian says: Hi there!"));
        commandProcessor.Post(new FarewellEvent("Ian says: See you later!"));
    }
}

public class CustomPublicationFinder : FindPublicationByPublicationTopicOrRequestType
{
    private static readonly Dictionary<Type, string> s_typeRouteMapper = new()
    {
        [typeof(GreetingEvent)] = "greeting.event",
        [typeof(FarewellEvent)] = "farewell.event"
    };

    public override Publication Find<TRequest>(IAmAProducerRegistry registry, RequestContext context)
    {
        if (s_typeRouteMapper.TryGetValue(typeof(TRequest), out var topic))
        {
            // If you have a tenant topic you could have this 
            // MAP: [typeof(A), "some-topic-{tenant}"
            // topic.Replace("{tenant}", tenantContext.Tenant)
            return registry.LookupBy(topic).Publication;
        }
            
        return base.Find<TRequest>(registry, context);
    }
}
