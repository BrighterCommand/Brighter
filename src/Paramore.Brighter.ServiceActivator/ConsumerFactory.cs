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
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.ServiceActivator
{
    internal sealed class ConsumerFactory<TRequest> : IConsumerFactory where TRequest : class, IRequest
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IAmAMessageMapperRegistry? _messageMapperRegistry;
        private readonly Subscription _subscription;
        private readonly IAmAMessageTransformerFactory? _messageTransformerFactory;
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly IAmABrighterTracer _tracer;
        private readonly InstrumentationOptions _instrumentationOptions;
        private readonly ConsumerName _consumerName;
        private readonly IAmAMessageMapperRegistryAsync? _messageMapperRegistryAsync;
        private readonly IAmAMessageTransformerFactoryAsync? _messageTransformerFactoryAsync;

        public ConsumerFactory(
            IAmACommandProcessor commandProcessor,
            Subscription subscription,
            IAmAMessageMapperRegistry messageMapperRegistry,
            IAmAMessageTransformerFactory? messageTransformerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmABrighterTracer tracer,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _commandProcessor = commandProcessor;
            _messageMapperRegistry = messageMapperRegistry;
            _subscription = subscription;
            _messageTransformerFactory = messageTransformerFactory ?? new EmptyMessageTransformerFactory();
            _requestContextFactory = requestContextFactory;
            _tracer = tracer;
            _instrumentationOptions = instrumentationOptions;
            _consumerName = new ConsumerName($"{_subscription.Name}-{Uuid.NewAsString()}");
        }
        
        public ConsumerFactory(
            IAmACommandProcessor commandProcessor,
            Subscription subscription,
            IAmAMessageMapperRegistryAsync messageMapperRegistryAsync,
            IAmAMessageTransformerFactoryAsync? messageTransformerFactoryAsync,
            IAmARequestContextFactory requestContextFactory,
            IAmABrighterTracer tracer,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _commandProcessor = commandProcessor;
            _messageMapperRegistryAsync = messageMapperRegistryAsync;
            _subscription = subscription;
            _messageTransformerFactoryAsync = messageTransformerFactoryAsync ?? new EmptyMessageTransformerFactoryAsync();
            _requestContextFactory = requestContextFactory;
            _tracer = tracer;
            _instrumentationOptions = instrumentationOptions;
            _consumerName = new ConsumerName($"{_subscription.Name}-{Uuid.NewAsString()}");
        }

        public Consumer Create()
        {
            if (_subscription.MessagePumpType == MessagePumpType.Proactor)
                return CreateProactor();
            else
                return CreateReactor();
        }

        private Consumer CreateReactor()
        {
            if (_messageMapperRegistry is null || _messageTransformerFactory is null)
                throw new ArgumentException("Message Mapper Registry and Transform factory must be set");
            
            if (_subscription.ChannelFactory is null)
                throw new ArgumentException("Subscription must have a Channel Factory in order to create a consumer.");
            
            var channel = _subscription.ChannelFactory.CreateSyncChannel(_subscription);
            var messagePump = new Reactor<TRequest>(_commandProcessor, _messageMapperRegistry, 
                _messageTransformerFactory, _requestContextFactory, channel, _tracer, _instrumentationOptions)
            {
                Channel = channel,
                TimeOut = _subscription.TimeOut,
                RequeueCount = _subscription.RequeueCount,
                RequeueDelay = _subscription.RequeueDelay,
                UnacceptableMessageLimit = _subscription.UnacceptableMessageLimit
            };

            return new Consumer(_consumerName, _subscription, channel, messagePump);
        }

        private Consumer CreateProactor()
        {
            if (_messageMapperRegistryAsync is null || _messageTransformerFactoryAsync is null)
                throw new ArgumentException("Message Mapper Registry and Transform factory must be set");

            if (_subscription.ChannelFactory is null)
                throw new ArgumentException("Subscription must have a Channel Factory in order to create a consumer.");
            
            var channel = _subscription.ChannelFactory.CreateAsyncChannel(_subscription);
            var messagePump = new Proactor<TRequest>(_commandProcessor, _messageMapperRegistryAsync, 
                _messageTransformerFactoryAsync, _requestContextFactory, channel, _tracer, _instrumentationOptions)
            {
                Channel = channel,
                TimeOut = _subscription.TimeOut,
                RequeueCount = _subscription.RequeueCount,
                RequeueDelay = _subscription.RequeueDelay,
                UnacceptableMessageLimit = _subscription.UnacceptableMessageLimit,
                EmptyChannelDelay = _subscription.EmptyChannelDelay,
                ChannelFailureDelay = _subscription.ChannelFailureDelay
            };

            return new Consumer(_consumerName, _subscription, channel, messagePump);
        }
    }
}
