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

using Common.Logging;
using Polly;

namespace paramore.brighter.commandprocessor
{
    public class CommandProcessorBuilder : INeedAHandlers, INeedPolicy, INeedLogging, INeedMessaging, INeedARequestContext, IAmACommandProcessorBuilder
    {
        private ILog logger;
        private IAmAMessageStore<Message> messageStore;
        private IAmAMessagingGateway messagingGateway;
        private IAmAMessageMapperRegistry messageMapperRegistry;
        private Policy retryPolicy;
        private Policy circuitBreakerPolicy;
        private IAmARequestContextFactory requestContextFactory;
        private IAmATargetHandlerRegistry registry;
        private IAmAHandlerFactory handlerFactory;
        private IAmAPolicyRegistry policyRegistry;
        private CommandProcessorBuilder() {}

        public static INeedAHandlers With()
        {
            return new CommandProcessorBuilder();
        }

        public INeedPolicy Handlers(HandlerConfiguration handlerConfiguration)
        {
            registry = handlerConfiguration.TargetHandlerRegistry;
            handlerFactory = handlerConfiguration.HandlerFactory;
            return this;
        }
        
        public INeedLogging Policies(IAmAPolicyRegistry thePolicyRegistry)
        {
            policyRegistry = thePolicyRegistry;
            return this;
        }

        public INeedLogging NoPolicy()
        {
            return this;
        }
        public INeedMessaging Logger(ILog logger)
        {
            this.logger = logger;
            return this;
        }

        public INeedARequestContext Messaging(MessagingConfiguration configuration)
        {
            this.messageStore = configuration.MessageStore;
            this.messagingGateway = configuration.MessagingGateway;
            this.messageMapperRegistry = configuration.MessageMapperRegistry;
            this.retryPolicy = configuration.RetryPolicy;
            this.circuitBreakerPolicy = configuration.CircuitBreakerPolicy;
            return this;
        }

        public INeedARequestContext NoMessaging()
        {
            return this;
        }

        public IAmACommandProcessorBuilder RequestContextFactory(IAmARequestContextFactory requestContextFactory)
        {
            this.requestContextFactory = requestContextFactory;
            return this;
        }

        public CommandProcessor Build()
        {
            return new CommandProcessor(
                targetHandlerRegistry: registry,
                handlerFactory: handlerFactory,
                requestContextFactory: requestContextFactory,
                policyRegistry: policyRegistry,
                mapperRegistry: messageMapperRegistry,
                messageStore: messageStore,
                messagingGateway: messagingGateway,
                retryPolicy: retryPolicy,
                circuitBreakerPolicy:circuitBreakerPolicy,
                logger: logger
                );
        }

    }

    #region Progressive interfaces
    public interface INeedAHandlers
    {
        INeedPolicy Handlers(HandlerConfiguration theRegistry);
    }

    public interface INeedPolicy
    {
        INeedLogging Policies(IAmAPolicyRegistry policyRegistry);
        INeedLogging NoPolicy();
    }

    public interface INeedLogging
    {
        INeedMessaging Logger(ILog logger);
    }

    public interface INeedMessaging
    {
        INeedARequestContext Messaging(MessagingConfiguration configuration);
        INeedARequestContext NoMessaging();
    }

    public interface INeedARequestContext
    {
        IAmACommandProcessorBuilder RequestContextFactory(IAmARequestContextFactory requestContextFactory);
    }
    public interface IAmACommandProcessorBuilder
    {
        CommandProcessor Build();
    }
    #endregion
}