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
using paramore.brighter.commandprocessor;

namespace HelloWorld
{
    class Program
    {

        static void Main(string[] args)
        {
            var properties = new NameValueCollection();
            properties["showDateTime"] = "true";
            LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);
            var logger = LogManager.GetLogger(typeof (Program));

            var registry = new SubscriberRegistry(); 
            registry.Register<GreetingCommand, GreetingCommandHandler>();


            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(
                     subscriberRegistry: registry,
                     handlerFactory: new SimpleHandlerFactory(logger)
                    ))
                .NoPolicy()
                .Logger(logger)
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();

            commandProcessor.Send(new GreetingCommand("Ian"));

            Console.ReadLine();
        }

        internal class SimpleHandlerFactory : IAmAHandlerFactory
        {
            private readonly ILog logger;

            public SimpleHandlerFactory(ILog logger)
            {
                this.logger = logger;
            }

            public IHandleRequests Create(Type handlerType)
            {
                return new GreetingCommandHandler(logger);
            }

            public void Release(IHandleRequests handler)
            {
            }
        }
    }

 
}
