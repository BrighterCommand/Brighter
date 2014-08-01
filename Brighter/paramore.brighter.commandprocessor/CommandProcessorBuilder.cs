// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
// <copyright file="CommandProcessorBuilder.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
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

/// <summary>
/// The commandprocessor namespace.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
/// </summary>
namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class CommandProcessorBuilder.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public class CommandProcessorBuilder : INeedAHandlers, INeedPolicy, INeedLogging, INeedMessaging, INeedARequestContext, IAmACommandProcessorBuilder
    {
        private ILog logger;
        private IAmAMessageStore<Message> messageStore;
        private IAmASendMessageGateway messagingGateway;
        private IAmAMessageMapperRegistry messageMapperRegistry;
        private IAmARequestContextFactory requestContextFactory;
        private IAmASubscriberRegistry registry;
        private IAmAHandlerFactory handlerFactory;
        private IAmAPolicyRegistry policyRegistry;
        private CommandProcessorBuilder() {}

        /// <summary>
        /// Withes this instance.
        /// </summary>
        /// <returns>INeedAHandlers.</returns>
        public static INeedAHandlers With()
        {
            return new CommandProcessorBuilder();
        }

        /// <summary>
        /// Handlerses the specified handler configuration.
        /// </summary>
        /// <param name="handlerConfiguration">The handler configuration.</param>
        /// <returns>INeedPolicy.</returns>
        public INeedPolicy Handlers(HandlerConfiguration handlerConfiguration)
        {
            registry = handlerConfiguration.SubscriberRegistry;
            handlerFactory = handlerConfiguration.HandlerFactory;
            return this;
        }

        /// <summary>
        /// Policieses the specified the policy registry.
        /// </summary>
        /// <param name="thePolicyRegistry">The policy registry.</param>
        /// <returns>INeedLogging.</returns>
        public INeedLogging Policies(IAmAPolicyRegistry thePolicyRegistry)
        {
            policyRegistry = thePolicyRegistry;
            return this;
        }

        /// <summary>
        /// Noes the policy.
        /// </summary>
        /// <returns>INeedLogging.</returns>
        public INeedLogging NoPolicy()
        {
            return this;
        }
        /// <summary>
        /// Loggers the specified logger.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <returns>INeedMessaging.</returns>
        public INeedMessaging Logger(ILog logger)
        {
            this.logger = logger;
            return this;
        }

        /// <summary>
        /// Tasks the queues.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns>INeedARequestContext.</returns>
        public INeedARequestContext TaskQueues(MessagingConfiguration configuration)
        {
            messageStore = configuration.MessageStore;
            messagingGateway = configuration.MessagingGateway;
            messageMapperRegistry = configuration.MessageMapperRegistry;
            return this;
        }

        /// <summary>
        /// Noes the task queues.
        /// </summary>
        /// <returns>INeedARequestContext.</returns>
        public INeedARequestContext NoTaskQueues()
        {
            return this;
        }

        /// <summary>
        /// Requests the context factory.
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <returns>IAmACommandProcessorBuilder.</returns>
        public IAmACommandProcessorBuilder RequestContextFactory(IAmARequestContextFactory requestContextFactory)
        {
            this.requestContextFactory = requestContextFactory;
            return this;
        }

        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <returns>CommandProcessor.</returns>
        public CommandProcessor Build()
        {
            return new CommandProcessor(
                subscriberRegistry: registry,
                handlerFactory: handlerFactory,
                requestContextFactory: requestContextFactory,
                policyRegistry: policyRegistry,
                mapperRegistry: messageMapperRegistry,
                messageStore: messageStore,
                messagingGateway: messagingGateway,
                logger: logger
                );
        }

    }

    #region Progressive interfaces
    /// <summary>
    /// Interface INeedAHandlers{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public interface INeedAHandlers
    {
        /// <summary>
        /// Handlerses the specified the registry.
        /// </summary>
        /// <param name="theRegistry">The registry.</param>
        /// <returns>INeedPolicy.</returns>
        INeedPolicy Handlers(HandlerConfiguration theRegistry);
    }

    /// <summary>
    /// Interface INeedPolicy{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public interface INeedPolicy
    {
        /// <summary>
        /// Policieses the specified policy registry.
        /// </summary>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <returns>INeedLogging.</returns>
        INeedLogging Policies(IAmAPolicyRegistry policyRegistry);
        /// <summary>
        /// Noes the policy.
        /// </summary>
        /// <returns>INeedLogging.</returns>
        INeedLogging NoPolicy();
    }

    /// <summary>
    /// Interface INeedLogging{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public interface INeedLogging
    {
        /// <summary>
        /// Loggers the specified logger.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <returns>INeedMessaging.</returns>
        INeedMessaging Logger(ILog logger);
    }

    /// <summary>
    /// Interface INeedMessaging{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public interface INeedMessaging
    {
        /// <summary>
        /// Tasks the queues.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns>INeedARequestContext.</returns>
        INeedARequestContext TaskQueues(MessagingConfiguration configuration);
        /// <summary>
        /// Noes the task queues.
        /// </summary>
        /// <returns>INeedARequestContext.</returns>
        INeedARequestContext NoTaskQueues();
    }

    /// <summary>
    /// Interface INeedARequestContext{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
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
    /// Interface IAmACommandProcessorBuilder{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
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