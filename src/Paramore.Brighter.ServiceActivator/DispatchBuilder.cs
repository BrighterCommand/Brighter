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
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Class DispatchBuilder.
    /// A fluent builder used to simplify construction of instances of the Dispatcher. Begin by calling With() and the syntax will then provide you with 
    /// progressive interfaces to manage the requirements for a complete Dispatcher via Intellisense in the IDE. The intent is to make it easier to
    /// recognize those dependencies that you need to configure
    /// </summary>
    public class DispatchBuilder : INeedACommandProcessorFactory, INeedAChannelFactory, INeedAMessageMapper, INeedAListOfSubcriptions, INeedObservability, IAmADispatchBuilder
    {
        private Func<IAmACommandProcessorProvider>? _commandProcessorFactory;
        private IAmAMessageMapperRegistry? _messageMapperRegistry;
        private IAmAMessageMapperRegistryAsync? _messageMapperRegistryAsync;
        private IAmAChannelFactory? _defaultChannelFactory;
        private IEnumerable<Subscription>? _subscriptions;
        private IAmAMessageTransformerFactory? _messageTransformerFactory;
        private IAmAMessageTransformerFactoryAsync? _messageTransformerFactoryAsync;
        private IAmARequestContextFactory? _requestContextFactory;
        private IAmABrighterTracer? _tracer;
        private InstrumentationOptions _instrumentationOptions;

        private DispatchBuilder() { }

        /// <summary>
        /// Begins the fluent interface 
        /// </summary>
        /// <returns>INeedALogger.</returns>
        public static INeedACommandProcessorFactory StartNew()
        {
            return new DispatchBuilder();
        }

        /// <summary>
        /// The command processor used to send and publish messages to handlers by the service activator.
        /// </summary>
        /// <param name="commandProcessorFactory">The command processor Factory.</param>
        /// <param name="requestContextFactory">The factory used to create a request synchronizationHelper for a pipeline</param>
        /// <returns>INeedAMessageMapper.</returns>
        public INeedAMessageMapper CommandProcessorFactory(
            Func<IAmACommandProcessorProvider> commandProcessorFactory,
            IAmARequestContextFactory requestContextFactory
            )
        {
            _commandProcessorFactory = commandProcessorFactory;
            _requestContextFactory = requestContextFactory;
            return this;
        }

        /// <summary>
        /// The message mappers used to map between commands, events, and on-the-wire handlers.
        /// </summary>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="messageMapperRegistryAsync">The async message mapper</param>
        /// <param name="messageTransformerFactory">A factory to produce transformers for a message mapper</param>
        /// <param name="messageTransformFactoryAsync">A factory to produce async transformers for a message mapper</param>
        /// <returns>INeedAChannelFactory.</returns>
        /// throws <see cref="ConfigurationException">You must provide at least one type of message mapper registry</see>
        public INeedAChannelFactory MessageMappers(
            IAmAMessageMapperRegistry messageMapperRegistry,
            IAmAMessageMapperRegistryAsync? messageMapperRegistryAsync,
            IAmAMessageTransformerFactory? messageTransformerFactory,
            IAmAMessageTransformerFactoryAsync?  messageTransformFactoryAsync)
        {
            _messageMapperRegistry = messageMapperRegistry;
            _messageMapperRegistryAsync = messageMapperRegistryAsync;
            _messageTransformerFactory = messageTransformerFactory;
            _messageTransformerFactoryAsync = messageTransformFactoryAsync;
            
            if (messageMapperRegistry is null && messageMapperRegistryAsync is null)
                throw new ConfigurationException("You must provide a message mapper registry or an async message mapper registry");
            
            return this;
        }

        /// <summary>
        /// The default channel factory - used to create channels. Generally an implementation of a specific Application Layer i.e.RabbitMQ for AMQP 
        /// needs to provide an implementation of this factory to provide input and output channels that support sending messages over that
        /// layer. We provide an implementation for RabbitMQ for example.
        /// </summary>
        /// <param name="defaultChannelFactory">The default channel factory that will be used if no Channel Factory is provided for each subscription.</param>
        /// <returns>INeedAListOfSubcriptions.</returns>
        public INeedAListOfSubcriptions ChannelFactory(IAmAChannelFactory defaultChannelFactory)
        {
            _defaultChannelFactory = defaultChannelFactory;
            return this;
        }
       
        /// <summary>
        /// Configures OpenTelemetry for the Dispatcher
        /// </summary>
        /// <param name="tracer">An instance of <see cref="BrighterTracer"/> with which to instrument the Dispatcher</param>
        /// <param name="instrumentationOptions">An <see cref="InstrumentationOptions"/> defining how verbose the instrumentation should be</param>
        /// <returns>INeedAListOfSubcriptions</returns>
        public IAmADispatchBuilder ConfigureInstrumentation(IAmABrighterTracer tracer, InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
             _tracer = tracer;
             _instrumentationOptions = instrumentationOptions;
             return this;
        }
        
        public IAmADispatchBuilder NoInstrumentation()
        {
            _tracer = null;
            _instrumentationOptions = InstrumentationOptions.None;
            return this;
        }

        /// <summary>
        /// A list of subscriptions i.e. mappings of channels to commands or events
        /// </summary>
        /// <param name="connections">The connections.</param>
        /// <returns>IAmADispatchBuilder.</returns>
        public INeedObservability Subscriptions(IEnumerable<Subscription> subscriptions)
        {
            _subscriptions = subscriptions;

            foreach (var connection in _subscriptions.Where(c => c.ChannelFactory == null))
            {
                connection.ChannelFactory = _defaultChannelFactory;
            }

            return this;
        }
        
        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <returns>Dispatcher.</returns>
        public Dispatcher Build()
        {
            if (_commandProcessorFactory is null || _subscriptions is null)
                throw new ArgumentException("Command Processor Factory and Subscription are required.");
            
            return new Dispatcher(_commandProcessorFactory, _subscriptions, _messageMapperRegistry, 
                _messageMapperRegistryAsync, _messageTransformerFactory, _messageTransformerFactoryAsync, 
                _requestContextFactory, _tracer, _instrumentationOptions
            );
        }


    }

    #region Progressive Interfaces

    /// <summary>
    /// Interface INeedACommandProcessor
    /// </summary>
    public interface INeedACommandProcessorFactory
    {
        /// <summary>
        /// The command processor used to send and publish messages to handlers by the service activator.
        /// </summary>
        /// <param name="commandProcessorFactory">The command processor provider Factory.</param>
        /// <param name="requestContextFactory">The factory used to create a request synchronizationHelper for a pipeline</param>
        /// <returns>INeedAMessageMapper.</returns>
        INeedAMessageMapper CommandProcessorFactory(
            Func<IAmACommandProcessorProvider> commandProcessorFactory,
            IAmARequestContextFactory requestContextFactory
            );
    }

    /// <summary>
    /// Interface INeedAMessageMapper
    /// </summary>
    public interface INeedAMessageMapper
    {
        /// <summary>
        /// The message mappers used to map between commands, events, and on-the-wire handlers.
        /// </summary>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="messageMapperRegistryAsync">The async message mapper registry</param>
        /// <param name="messageTransformerFactory">The factory for creating transforms</param>
        /// <param name="messageTransformFactoryAsync">The factory for creating async transforms</param>
        /// <returns>INeedAChannelFactory.</returns>
        INeedAChannelFactory MessageMappers(
            IAmAMessageMapperRegistry messageMapperRegistry,
            IAmAMessageMapperRegistryAsync? messageMapperRegistryAsync,
            IAmAMessageTransformerFactory? messageTransformerFactory,
            IAmAMessageTransformerFactoryAsync?  messageTransformFactoryAsync);
    }
    /// <summary>
    /// Interface INeedAChannelFactory
    /// </summary>
    public interface INeedAChannelFactory
    {
        /// <summary>
        /// The channel factory - used to create channels. Generally an implementation of a specific Application Layer i.e.RabbitMQ for AMQP 
        /// needs to provide an implementation of this factory to provide input and output channels that support sending messages over that
        /// layer. We provide an implementation for RabbitMQ for example.
        /// </summary>
        /// <param name="defaultChannelFactory">The channel factory.</param>
        /// <returns>INeedAListOfSubcriptions.</returns>
        INeedAListOfSubcriptions ChannelFactory(IAmAChannelFactory defaultChannelFactory);
    }

    /// <summary>
    /// Interface INeedAListOfSubcriptions
    /// </summary>
    public interface INeedAListOfSubcriptions
    {
        /// <summary>
        /// A list of connections i.e. mappings of channels to commands or events
        /// </summary>
        /// <returns>IAmADispatchBuilder.</returns>
        INeedObservability Subscriptions(IEnumerable<Subscription> subscriptions);
    }

    public interface INeedObservability
    {
        /// <summary>
        /// Sets the InstrumentationOptions for the Dispatcher
        /// </summary>
        /// <param name="tracer">The tracer that we should use to create telemetry</param>
        /// <param name="instrumentationOptions">What depth of instrumentation do we want.
        /// InstrumentationOptions.None - no telemetry
        /// InstrumentationOptions.RequestInformation - id  and type of request
        /// InstrumentationOptions.RequestBody -  body of the request
        /// InstrumentationOptions.RequestContext - what is the synchronizationHelper of the request
        /// InstrumentationOptions.All - all of the above
        /// </param>
        /// <returns>IAmADispatchBuilder</returns>
        IAmADispatchBuilder ConfigureInstrumentation(IAmABrighterTracer tracer, InstrumentationOptions instrumentationOptions = InstrumentationOptions.All);
       
        /// <summary>
        /// We do not need any instrumentation for the Dispatcher
        /// </summary>
        /// <returns>IAmADispatchBuilder</returns>
        IAmADispatchBuilder NoInstrumentation();
    } 

    /// <summary>
    /// Interface IAmADispatchBuilder
    /// </summary>
    public interface IAmADispatchBuilder
    {
        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <returns>Dispatcher.</returns>
        Dispatcher Build();
    }
    #endregion
}
