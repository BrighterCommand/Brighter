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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Publish
{
    [Collection("CommandProcessor")]
    public class PublishingToMultipleSubscribersAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyEvent _myEvent = new();
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private Exception? _exception;

        public PublishingToMultipleSubscribersAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
            registry.RegisterAsync<MyEvent, MyOtherEventHandlerAsync>();
            registry.RegisterAsync<MyEvent, MyThrowingEventHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyEventHandlerAsync>();
            container.AddTransient<MyOtherEventHandlerAsync>();
            container.AddTransient<MyThrowingEventHandlerAsync>();
            container.AddSingleton(_receivedMessages);
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());


            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            PipelineBuilder<MyEvent>.ClearPipelineCache();
        }

        [Fact]
        public async Task When_Publishing_To_Multiple_Subscribers_Should_Aggregate_Exceptions_Async()
        {
            _exception = await Catch.ExceptionAsync(async () => await _commandProcessor.PublishAsync(_myEvent));

            //Should throw an aggregate exception
            Assert.IsType<AggregateException>(_exception);
            //Should have an inner exception from the handler
            Assert.IsType<InvalidOperationException>(((AggregateException)_exception).InnerException);
            //Should publish the command to the first event handler
            Assert.Contains(new KeyValuePair<string, string>(nameof(MyEventHandlerAsync), _myEvent.Id), _receivedMessages);
            //Should publish the command to the second event handler
            Assert.Contains(new KeyValuePair<string, string>(nameof(MyOtherEventHandlerAsync), _myEvent.Id), _receivedMessages);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
