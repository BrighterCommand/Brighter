// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
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

using System;
using System.Linq;
using System.Reflection;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class RequestHandler.
    /// A target of the <see cref="CommandProcessor"/> either as the target of the Command Dispatcher to provide the domain logic required to handle the <see cref="Command"/>
    /// or <see cref="Event"/> or as an orthogonal handler used as part of the Command Processor pipeline.
    /// We recommend deriving your concrete handler from <see cref="RequestHandler{T}"/> instead of implementing the interface as it provides boilerplate
    /// code for calling the next handler in sequence in the pipeline and describing the path
    /// By default the <see cref="Name"/> is based of the Type name, and the <see cref="DescribePath"/> adds that <see cref="Name"/> into the <see cref="IAmAPipelineTracer"/> list.
    /// By default the <see cref="Handle"/> method will log the calls and forward the call to the handler's <see cref="Successor"/>. You should call 
    /// <code>
    /// base.Handle(command); 
    /// </code>
    /// within your derived class handler to forward the call to the next handler in the chain.
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    public abstract class RequestHandler<TRequest> : IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        private static Lazy<ILog> s_logger;
        private IHandleRequests<TRequest> _successor;

        /// <summary>
        /// The logger
        /// </summary>
        protected ILog Logger { get { return s_logger.Value; }}

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        protected RequestHandler()
        {
            s_logger = new Lazy<ILog>(() => LogProvider.GetCurrentClassLogger());
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// Generally you can should prefer the default constructor, and we will grab the logger from your log provider rather than take a direct dependency.
        /// This can be helpful for testing.
        /// </summary>
        /// <param name="logger">The logger.</param>
        protected RequestHandler(ILog logger)
        {
            s_logger = new Lazy<ILog>(() => logger);
        }

        /// <summary>
        /// Gets or sets the context.
        /// </summary>
        /// <value>The context.</value>
        public IRequestContext Context { get; set; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public HandlerName Name
        {
            get { return new HandlerName(GetType().Name); }
        }

        /// <summary>
        /// Sets the successor.
        /// </summary>
        /// <param name="successor">The successor.</param>
        public void SetSuccessor(IHandleRequests<TRequest> successor)
        {
            _successor = successor;
        }

        /// <summary>
        /// Adds to lifetime.
        /// </summary>
        /// <param name="instanceScope">The instance scope.</param>
        public void AddToLifetime(IAmALifetime instanceScope)
        {
            instanceScope.Add(this);

            if (_successor != null)
                _successor.AddToLifetime(instanceScope);
        }

        /// <summary>
        /// Describes the path.
        /// </summary>
        /// <param name="pathExplorer">The path explorer.</param>
        public void DescribePath(IAmAPipelineTracer pathExplorer)
        {
            pathExplorer.AddToPath(Name);
            if (_successor != null)
                _successor.DescribePath(pathExplorer);
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public virtual TRequest Handle(TRequest command)
        {
            if (_successor != null)
            {
                Logger.DebugFormat("Passing request from {0} to {1}", Name, _successor.Name);
                return _successor.Handle(command);
            }

            return command;
        }

        /// <summary>
        /// If a request cannot be completed by <see cref="Handle"/>, implementing the <see cref="Fallback"/> method provides an alternate code path that can be used
        /// This allows for graceful  degradation. Using the <see cref="FallbackPolicyAttribute"/> handler you can configure a policy to catch either all <see cref="Exception"/>'s or
        /// just <see cref="BrokenCircuitException"/> that occur later in the pipeline, and then call the <see cref="Fallback"/> path.
        /// Note that the <see cref="FallbackPolicyAttribute"/> target handler might be 'beginning of chain' and need to pass through to actual handler that is end of chain.
        /// Because of this we need to call Fallback on the chain. Later step handlers don't know the context of failure so they cannot know if any operations they had, 
        /// that could fail (such as DB access) were the cause of the failure chain being hit.
        /// Steps that don't know how to handle should pass through.
        /// Useful alternatives for Fallback are to try via the cache.
        /// Note that a Fallback handler implementation should not catch exceptions in the <see cref="Fallback"/> chain to avoid an infinite loop.
        /// Call <see cref="Successor"/>.<see cref="Handle"/> if having provided a Fallback you want the chain to return to the 'happy' path. Excerise caution here though
        /// as you do not know who generated the exception that caused the fallback chain.
        /// For this reason, the <see cref="FallbackPolicyHandler"/> puts the exception in the request context.
        /// When the <see cref="FallbackPolicyAttribute"/> is set on the <see cref="Handle"/> method of a derived class
        /// The <see cref="FallbackPolicyHandler{TRequest}"/> will catch either all failures (backstop) or <see cref="BrokenCircuitException"/> depending on configuration
        /// and call the <see cref="RequestHandler{TRequest}"/>'s <see cref="Fallback"/> method
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public virtual TRequest Fallback(TRequest command)
        {
            if (_successor != null)
            {
                Logger.DebugFormat("Falling back from {0} to {1}", Name, _successor.Name);
                return _successor.Fallback(command);
            }
            return command;
        }

        //default is just to do nothing - use this if you need to pass data from an attribute into a handler
        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public virtual void InitializeFromAttributeParams(params object[] initializerList) { }


        internal MethodInfo FindHandlerMethod()
        {
            var methods = GetType().GetMethods();
            return methods
                .Where(method => method.Name == "Handle")
                .SingleOrDefault(method => method.GetParameters().Count() == 1 && method.GetParameters().Single().ParameterType == typeof(TRequest));
        }

    }
}
