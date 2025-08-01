﻿#region Licence
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
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Publish
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPublishMultipleMatchesAgreementTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private Exception? _exception;

        public CommandProcessorPublishMultipleMatchesAgreementTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyEvent>((request, context) =>
            {
                var myEvent = request as MyEvent;
                var handlerList = new List<Type>();
                
                if (myEvent.Data == 4)
                    handlerList.Add(typeof(MyEventHandler));
                
                if (myEvent.Data > 2)
                    handlerList.Add(typeof(MyOtherEventHandler));
                
                return handlerList;
                    
            }, [typeof(MyEventHandler), typeof(MyOtherEventHandler)]);

            var container = new ServiceCollection();
            container.AddTransient<MyEventHandler>();
            container.AddTransient<MyOtherEventHandler>();
            container.AddSingleton(_receivedMessages);
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});


            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new InMemorySchedulerFactory());
            PipelineBuilder<MyEvent>.ClearPipelineCache();
        }

        [Fact]
        public void When_There_Are_Multiple_Subscribers()
        {
            var myEvent = new MyEvent
            {
                Data = 4 // This will match both handlers
            };
            
            _exception = Catch.Exception(() => _commandProcessor.Publish(myEvent));

            //Should not throw an exception
            Assert.Null(_exception);
            //Should publish the command to the first event handler
            Assert.Contains(new KeyValuePair<string, string>(nameof(MyEventHandler), myEvent.Id), _receivedMessages);
            //Should publish the command to the second event handler
            Assert.Contains(new KeyValuePair<string, string>(nameof(MyOtherEventHandler), myEvent.Id), _receivedMessages);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
