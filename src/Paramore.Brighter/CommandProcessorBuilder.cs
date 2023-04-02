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

using System.Collections.Generic;
using Paramore.Brighter.FeatureSwitch;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class CommandProcessorBuilder.
    /// Provides a fluent interface to construct a <see cref="CommandProcessor"/>. We need to identify the following dependencies in order to create a <see cref="CommandProcessor"/>
    /// <list type="bullet">
    ///     <item>
    ///         <description>
    ///             A <see cref="HandlerConfiguration"/> containing a <see cref="IAmASubscriberRegistry"/> and a <see cref="IAmAHandlerFactory"/>. You can use <see cref="SubscriberRegistry"/>
    ///             to provide the <see cref="IAmASubscriberRegistry"/> but you need to implement your own  <see cref="IAmAHandlerFactory"/>, for example using your preferred Inversion of Control
    ///             (IoC) container
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             A <see cref="IPolicyRegistry{TKey}"/> containing a list of policies that you want to be accessible to the <see cref="CommandProcessor"/>. You can use
    ///             <see cref="PolicyRegistry"/> to provide the <see cref="IPolicyRegistry{TKey}"/>. Policies are expected to be Polly <see cref="!:https://github.com/michael-wolfenden/Polly"/> 
    ///             <see cref="Paramore.Brighter.Policies"/> references.
    ///             If you do not need any policies around quality of service (QoS) concerns - you do not have Work Queues and/or do not intend to use Polly Policies for 
    ///             QoS concerns - you can use <see cref="DefaultPolicy"/> to indicate you do not need them or just want a simple retry.
    ///         </description>
    ///      </item>
    ///     <item>
    ///         <description>
    ///             A <see cref="ExternalBusConfiguration"/> describing how you want to configure Task Queues for the <see cref="CommandProcessor"/>. We store messages in a <see cref="IAmAnOutbox{T}"/>
    ///             for later replay (in case we need to compensate by trying a message again). We send messages to a Task Queue via a <see cref="IAmAMessageProducer"/> and we  want to know how
    ///             to map the <see cref="IRequest"/> (<see cref="Command"/> or <see cref="Event"/>) to a <see cref="Message"/> using a <see cref="IAmAMessageMapper"/> using 
    ///             an <see cref="IAmAMessageMapperRegistry"/>. You can use the default <see cref="MessageMapperRegistry"/> to register the association. You need to 
    ///             provide a <see cref="IAmAMessageMapperFactory"/> so that we can create instances of your  <see cref="IAmAMessageMapper"/>. You need to provide a <see cref="IAmAMessageMapperFactory"/>
    ///             when using <see cref="MessageMapperRegistry"/> so that we can create instances of your mapper. 
    ///             If you don't want to use Task Queues i.e. you are just using a synchronous Command Dispatcher approach, then use the <see cref="NoExternalBus"/> method to indicate your intent
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             Finally we need to provide a <see cref="IRequestContext"/> to provide context to requests handlers in the pipeline that can be used to pass information without using the message
    ///             that initiated the pipeline. We instantiate this via a user-provided <see cref="IAmARequestContextFactory"/>. The default approach is use <see cref="InMemoryRequestContextFactory"/>
    ///             to provide a <see cref="RequestContext"/> unless you have a requirement to replace this, such as in testing.
    ///         </description>
    ///     </item>
    /// </list> 
    /// </summary>
    public class CommandProcessorBuilder : INeedAHandlers, INeedPolicy, INeedMessaging, INeedARequestContext, IAmACommandProcessorBuilder
    {
        private IAmAnOutbox<Message> _outbox;
        private IAmAProducerRegistry _producers;
        private IAmAMessageMapperRegistry _messageMapperRegistry;
        private IAmAMessageTransformerFactory _transformerFactory;
        private IAmARequestContextFactory _requestContextFactory;
        private IAmASubscriberRegistry _registry;
        private IAmAHandlerFactory _handlerFactory;
        private IPolicyRegistry<string> _policyRegistry;
        private IAmAFeatureSwitchRegistry _featureSwitchRegistry;
        private IAmAChannelFactory _responseChannelFactory;
        private int _outboxWriteTimeout;
        private bool _useExternalBus = false;
        private bool _useRequestReplyQueues = false;
        private IEnumerable<Subscription> _replySubscriptions;
        private IAmABoxTransactionConnectionProvider _overridingBoxTransactionConnectionProvider = null;
        private int _outboxBulkChunkSize;

        private CommandProcessorBuilder()
        {
            DefaultPolicy();
        }

        /// <summary>
        /// Begins the Fluent Interface
        /// </summary>
        /// <returns>INeedAHandlers.</returns>
        public static INeedAHandlers With()
        {
            return new CommandProcessorBuilder();
        }

        /// <summary>
        /// Supplies the specified handler configuration, so that we can register subscribers and the handler factory used to create instances of them
        /// </summary>
        /// <param name="handlerConfiguration">The handler configuration.</param>
        /// <returns>INeedPolicy.</returns>
        public INeedPolicy Handlers(HandlerConfiguration handlerConfiguration)
        {
            _registry = handlerConfiguration.SubscriberRegistry;
            _handlerFactory = handlerConfiguration.HandlerFactory;
            return this;
        }

        /// <summary>
        /// Supplies the specified feature switching configuration, so we can use feature switches on user-defined request handlers
        /// </summary>
        /// <param name="featureSwitchRegistry">The feature switch config provider</param>
        /// <returns>INeedPolicy</returns>
        public INeedAHandlers ConfigureFeatureSwitches(IAmAFeatureSwitchRegistry featureSwitchRegistry)
        {
            _featureSwitchRegistry = featureSwitchRegistry;
            return this;
        }

        /// <summary>
        /// Supplies the specified the policy registry, so we can use policies for Task Queues or in user-defined request handlers such as ExceptionHandler
        /// that provide quality of service concerns
        /// </summary>
        /// <param name="thePolicyRegistry">The policy registry.</param>
        /// <returns>INeedLogging.</returns>
        /// <exception cref="ConfigurationException">The policy registry is missing the CommandProcessor.RETRYPOLICY policy which is required</exception>
        /// <exception cref="ConfigurationException">The policy registry is missing the CommandProcessor.CIRCUITBREAKER policy which is required</exception>
        public INeedMessaging Policies(IPolicyRegistry<string> thePolicyRegistry)
        {
            if (!thePolicyRegistry.ContainsKey(CommandProcessor.RETRYPOLICY))
                throw new ConfigurationException("The policy registry is missing the CommandProcessor.RETRYPOLICY policy which is required");
                
            if (!thePolicyRegistry.ContainsKey(CommandProcessor.CIRCUITBREAKER))
                throw new ConfigurationException("The policy registry is missing the CommandProcessor.CIRCUITBREAKER policy which is required");
            
            _policyRegistry = thePolicyRegistry;
            return this;
        }

        /// <summary>
        /// Use this if you do not require a policy and only want to retry once(i.e. No Tasks Queues or QoS needs).
        /// </summary>
        /// <returns>INeedLogging.</returns>
        public INeedMessaging DefaultPolicy()
        {
            _policyRegistry = new DefaultPolicy();
            return this;
        }

        /// <summary>
        /// The <see cref="CommandProcessor"/> wants to support <see cref="CommandProcessor.Post{T}(T)"/> or <see cref="CommandProcessor.Repost"/> using an external bus.
        /// You need to provide a policy to specify how QoS issues, specifically <see cref="CommandProcessor.RETRYPOLICY "/> or <see cref="CommandProcessor.CIRCUITBREAKER "/> 
        /// are handled by adding appropriate <see cref="Policies"/> when choosing this option.
        /// 
        /// </summary>
        /// <param name="configuration">The Task Queues configuration.</param>
        /// <param name="outbox">The Outbox.</param>
        /// <param name="boxTransactionConnectionProvider"></param>
        /// <returns>INeedARequestContext.</returns>
        public INeedARequestContext ExternalBus(ExternalBusConfiguration configuration, IAmAnOutbox<Message> outbox, IAmABoxTransactionConnectionProvider boxTransactionConnectionProvider = null)
        {
            _useExternalBus = true;
            _producers = configuration.ProducerRegistry;
            _outbox = outbox;
            _overridingBoxTransactionConnectionProvider = boxTransactionConnectionProvider;
            _messageMapperRegistry = configuration.MessageMapperRegistry;
            _outboxWriteTimeout = configuration.OutboxWriteTimeout;
            _outboxBulkChunkSize = configuration.OutboxBulkChunkSize;
            _transformerFactory = configuration.TransformerFactory;
            return this;
        }

        /// <summary>
        /// Use to indicate that you are not using Task Queues.
        /// </summary>
        /// <returns>INeedARequestContext.</returns>
        public INeedARequestContext NoExternalBus()
        {
            return this;
        }

        /// <summary>
        /// The <see cref="CommandProcessor"/> wants to support <see cref="CommandProcessor.Call{T}(T)"/> using RPC between client and server 
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="outbox">The outbox</param>
        /// <param name="subscriptions">Subscriptions for creating reply queues</param>
        /// <returns></returns>
        public INeedARequestContext ExternalRPC(ExternalBusConfiguration configuration, IAmAnOutbox<Message> outbox, IEnumerable<Subscription> subscriptions)
        {
            _useRequestReplyQueues = true;
            _replySubscriptions = subscriptions;
            _producers = configuration.ProducerRegistry;
            _messageMapperRegistry = configuration.MessageMapperRegistry;
            _outboxWriteTimeout = configuration.OutboxWriteTimeout;
            _responseChannelFactory = configuration.ResponseChannelFactory;
            _outbox = outbox;
            _transformerFactory = configuration.TransformerFactory;
             
            return this;
        }

        /// <summary>
        /// The factory for <see cref="IRequestContext"/> used within the pipeline to pass information between <see cref="IHandleRequests{T}"/> steps. If you do not need to override
        /// provide <see cref="InMemoryRequestContextFactory"/>.
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <returns>IAmACommandProcessorBuilder.</returns>
        public IAmACommandProcessorBuilder RequestContextFactory(IAmARequestContextFactory requestContextFactory)
        {
            _requestContextFactory = requestContextFactory;
            return this;
        }

        /// <summary>
        /// Builds the <see cref="CommandProcessor"/> from the configuration.
        /// </summary>
        /// <returns>CommandProcessor.</returns>
        public CommandProcessor Build()
        {
            if (!(_useExternalBus || _useRequestReplyQueues))
            {
                return new CommandProcessor(
                    
                    subscriberRegistry: _registry,
                    handlerFactory: _handlerFactory,
                    requestContextFactory: _requestContextFactory,
                    policyRegistry: _policyRegistry,
                    featureSwitchRegistry: _featureSwitchRegistry);
            }
            else if (_useExternalBus)
            {
                return new CommandProcessor(
                    subscriberRegistry: _registry,
                    handlerFactory: _handlerFactory,
                    requestContextFactory: _requestContextFactory,
                    policyRegistry: _policyRegistry,
                    mapperRegistry: _messageMapperRegistry,
                    outBox: _outbox,
                    producerRegistry: _producers,
                    outboxTimeout: _outboxWriteTimeout,
                    featureSwitchRegistry: _featureSwitchRegistry,
                    boxTransactionConnectionProvider: _overridingBoxTransactionConnectionProvider,
                    outboxBulkChunkSize: _outboxBulkChunkSize,
                    messageTransformerFactory: _transformerFactory
                );
            }
            else if (_useRequestReplyQueues)
            {
                 return new CommandProcessor(
                    subscriberRegistry: _registry,
                    handlerFactory: _handlerFactory,
                    requestContextFactory: _requestContextFactory,
                    policyRegistry: _policyRegistry,
                    mapperRegistry: _messageMapperRegistry,
                    outBox: _outbox,
                    producerRegistry: _producers,
                    replySubscriptions: _replySubscriptions,
                    responseChannelFactory: _responseChannelFactory, boxTransactionConnectionProvider: _overridingBoxTransactionConnectionProvider);
            }
            else
            {
                throw new ConfigurationException("Unknown Command Processor Type");
            }
        }
    }

    #region Progressive interfaces
    /// <summary>
    /// Interface INeedAHandlers
    /// </summary>
    public interface INeedAHandlers
    {
        /// <summary>
        /// Handlers the specified the registry.
        /// </summary>
        /// <param name="theRegistry">The registry.</param>
        /// <returns>INeedPolicy.</returns>
        INeedPolicy Handlers(HandlerConfiguration theRegistry);
        
        /// <summary>
        /// Configure Feature Switches for the Handlers
        /// </summary>
        /// <param name="featureSwitchRegistry"></param>
        /// <returns></returns>
        INeedAHandlers ConfigureFeatureSwitches(IAmAFeatureSwitchRegistry featureSwitchRegistry);
    }

    /// <summary>
    /// Interface INeedPolicy
    /// </summary>
    public interface INeedPolicy
    {
        /// <summary>
        /// Policies the specified policy registry.
        /// </summary>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <returns>INeedLogging.</returns>
        INeedMessaging Policies(IPolicyRegistry<string> policyRegistry);
        /// <summary>
        /// Knows the policy.
        /// </summary>
        /// <returns>INeedMessaging.</returns>
        INeedMessaging DefaultPolicy();
    }

  
    /// <summary>
    /// Interface INeedMessaging
    /// Note that a single command builder does not support both task queues and rpc, using the builder
    /// </summary>
    public interface INeedMessaging
    {
        /// <summary>
        /// Configure a task queue to send messages out of process
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="outbox">The outbox.</param>
        /// <param name="boxTransactionConnectionProvider">The connection provider to use when adding messages to the bus</param>
        /// <returns>INeedARequestContext.</returns>
        INeedARequestContext ExternalBus(ExternalBusConfiguration configuration, IAmAnOutbox<Message> outbox, IAmABoxTransactionConnectionProvider boxTransactionConnectionProvider = null);
        /// <summary>
        /// We don't send messages out of process
        /// </summary>
        /// <returns>INeedARequestContext.</returns>
        INeedARequestContext NoExternalBus();

        /// <summary>
        ///  We want to use RPC to send messages to another process
        /// </summary>
        /// <param name="externalBusConfiguration"></param>
        /// <param name="outboxSync">The outbox</param>
        /// <param name="subscriptions">Subscriptions for creating Reply queues</param>
        INeedARequestContext ExternalRPC(ExternalBusConfiguration externalBusConfiguration, IAmAnOutbox<Message> outboxSync, IEnumerable<Subscription> subscriptions);
    }

    /// <summary>
    /// Interface INeedARequestContext
    /// </summary>
    public interface INeedARequestContext
    {
        /// <summary>
        /// Requests the context factory.
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <returns>IAmACommandProcessorBuilder.</returns>
        IAmACommandProcessorBuilder RequestContextFactory(IAmARequestContextFactory requestContextFactory);
    }
    
    /// <summary>
    /// Interface IAmACommandProcessorBuilder
    /// </summary>
    public interface IAmACommandProcessorBuilder
    {
        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <returns>CommandProcessor.</returns>
        CommandProcessor Build();
    }
    #endregion
}
