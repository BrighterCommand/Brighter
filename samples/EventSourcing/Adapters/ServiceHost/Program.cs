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
using EventSourcing.ManualTinyIoc;
//using System.Reflection;
using EventSourcing.Ports.CommandHandlers;
using EventSourcing.Ports.Commands;
using Newtonsoft.Json;
using Paramore.Brighter;
using Paramore.Brighter.CommandStore.MsSql;

namespace EventSourcing.Adapters.ServiceHost
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            //var dbPath = Path.Combine(Path.GetDirectoryName(typeof(GreetingCommandHandler).GetTypeInfo().Assembly.GetName().FullName), "App_Data\\CommandStore.sdf");
            //var connectionString = "DataSource=\"" + dbPath + "\"";
            string connectionString = "";
             var configuration = new MsSqlCommandStoreConfiguration(connectionString, "Commands");
            var commandStore = new MsSqlCommandStore(configuration);

            var registry = new SubscriberRegistry();
            registry.Register<GreetingCommand, GreetingCommandHandler>();

            var tinyIoCContainer = new TinyIoCContainer();
            tinyIoCContainer.Register<IHandleRequests<GreetingCommand>, GreetingCommandHandler>();
            tinyIoCContainer.Register<IAmACommandStore>(commandStore);

            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(
                     subscriberRegistry: registry,
                     handlerFactory: new TinyIocHandlerFactory(tinyIoCContainer)
                    ))
                .DefaultPolicy()
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();

            var greetingCommand = new GreetingCommand("Ian");

            commandProcessor.Send(greetingCommand);

            var retrievedCommand = commandStore.Get<GreetingCommand>(greetingCommand.Id);

            var commandAsJson = JsonConvert.SerializeObject(retrievedCommand);

            Console.WriteLine(string.Format("Command retrieved from store: {0}", commandAsJson));

            Console.ReadLine();
        }

    }
}
