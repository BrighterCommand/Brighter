#region Licence

/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;

namespace HelloWorldAsync
{
    internal class Program
    {
        private static void Main()
        {
            // Use bootstrapper for async/await in Console app
            MainAsync().Wait();
        }

        private static async Task MainAsync()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<GreetingCommand, GreetingCommandRequestHandlerAsync>();

            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(registry, new SimpleAsyncHandlerFactory()))
                .DefaultPolicy()
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();

            // To allow the command handler(s) to release the thread
            // while doing potentially long running operations,
            // await the async (but inline) handling of the command
            await commandProcessor.SendAsync(new GreetingCommand("Ian"));

            Console.ReadLine();
        }

        internal class SimpleAsyncHandlerFactory : IAmAHandlerFactoryAsync
        {
            public void Release(IHandleRequestsAsync handler)
            {
            }

            IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType)
            {
                return new GreetingCommandRequestHandlerAsync();
            }
        }
    }
}