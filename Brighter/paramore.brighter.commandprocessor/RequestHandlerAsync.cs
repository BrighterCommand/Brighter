// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 2015-12-21
//                    Based on RequestHandler.cs
//
// Last Modified By : Fred
// Last Modified On : 2015-12-21
//                    Based on RequestHandler
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
using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.policy.Attributes;
using paramore.brighter.commandprocessor.policy.Handlers;
using Polly.CircuitBreaker;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class RequestHandlerAsync    
    /// A target of the <see cref="CommandProcessor"/> either as the target of the Command Dispatcher to provide the domain logic required to handle the <see cref="Command"/>
    /// or <see cref="Event"/> or as an orthogonal handler used as part of the Command Processor pipeline.
    /// We recommend deriving your concrete handler from <see cref="RequestHandlerAsync{T}"/> instead of implementing the interface as it provides boilerplate
    /// code for calling the next handler in sequence in the pipeline and describing the path
    /// By default the <see cref="Name"/> is based of the Type name, and the <see cref="DescribePath"/> adds that <see cref="Name"/> into the <see cref="IAmAPipelineTracer"/> list.
    /// By default the <see cref="Handle"/> method will log the calls and forward the call to the handler's <see cref="Successor"/>. You should call 
    /// <code>
    /// await base.Handle(command); 
    /// </code>
    /// within your derived class handler to forward the call to the next handler in the chain.
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    public abstract class RequestHandlerAsync<TRequest> : IHandleRequestsAsync<TRequest> where TRequest : class, IRequest
    {

        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILog logger;


        private IHandleRequestsAsync<TRequest> _successor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandlerAsync{TRequest}"/> class.
        /// </summary>
        protected RequestHandlerAsync() 
            : this(LogProvider.For<RequestHandlerAsync<TRequest>>())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandlerAsync{TRequest}"/> class.
        /// Generally you can should prefer the default constructor, and we will grab the logger from your log provider rather than take a direct dependency.
        /// This can be helpful for testing.
        /// </summary>
        /// <param name="logger">The logger.</param>
        protected RequestHandlerAsync(ILog logger)
        {
            ContinueOnCapturedContext = false;
            this.logger = logger;
        }

        /// <summary>
        /// Gets or sets the context.
        /// </summary>
        /// <value>The context.</value>
        public IRequestContext Context { get; set; }

        /// <summary>
        /// If false we use a thread from the thread pool to run any continuation, if true we use the originating thread.
        /// Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait 
        /// or access the Result or otherwise block. You may need the orginating thread if you need to access thread specific storage
        /// such as HTTPContext 
        /// </summary>
        /// 
        public bool ContinueOnCapturedContext { get; set; }

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
        public void SetSuccessor(IHandleRequestsAsync<TRequest> successor)
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
        /// Awaitably handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="ct">A cancellation token (optional). Can be used to signal that the pipeline should end by the caller</param>
        /// <returns>Awaitable <see cref="Task{TRequest}"/>.</returns>
        public virtual async Task<TRequest> HandleAsync(TRequest command, CancellationToken? ct = null)
        {
            if (_successor != null)
            {
                logger.DebugFormat("Passing request from {0} to {1}", Name, _successor.Name);
                return await _successor.HandleAsync(command, ct).ConfigureAwait(ContinueOnCapturedContext);
            }

            return command;
        }

        /// <summary>
        /// If a request cannot be completed by <see cref="HandleAsync"/>, implementing the <see cref="FallbackAsync"/> method provides an alternate code path that can be used
        /// This allows for graceful  degradation. Using the <see cref="FallbackPolicyAttribute"/> handler you can configure a policy to catch either all <see cref="Exception"/>'s or
        /// just <see cref="BrokenCircuitException"/> that occur later in the pipeline, and then call the <see cref="FallbackAsync"/> path.
        /// Note that the <see cref="FallbackPolicyAttribute"/> target handler might be 'beginning of chain' and need to pass through to actual handler that is end of chain.
        /// Because of this we need to call Fallback on the chain. Later step handlers don't know the context of failure so they cannot know if any operations they had, 
        /// that could fail (such as DB access) were the cause of the failure chain being hit.
        /// Steps that don't know how to handle should pass through.
        /// Useful alternatives for Fallback are to try via the cache.
        /// Note that a Fallback handler implementation should not catch exceptions in the <see cref="FallbackAsync"/> chain to avoid an infinite loop.
        /// Call <see cref="Successor"/>.<see cref="HandleAsync"/> if having provided a Fallback you want the chain to return to the 'happy' path. Excerise caution here though
        /// as you do not know who generated the exception that caused the fallback chain.
        /// For this reason, the <see cref="FallbackPolicyHandler"/> puts the exception in the request context.
        /// When the <see cref="FallbackPolicyAttribute"/> is set on the <see cref="HandleAsync"/> method of a derived class
        /// The <see cref="FallbackPolicyHandler{TRequest}"/> will catch either all failures (backstop) or <see cref="BrokenCircuitException"/> depending on configuration
        /// and call the <see cref="RequestHandler{TRequest}"/>'s <see cref="FallbackAsync"/> method
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="ct">A cancellation token (optional). Can be used to signal that the pipeline should end by the caller</param>
        /// <returns>Awaitable <see cref="Task{TRequest}"/>.</returns>
        public virtual async Task<TRequest> FallbackAsync(TRequest command, CancellationToken? ct = null)
        {
            if (_successor != null)
            {
                logger.DebugFormat("Falling back from {0} to {1}", Name, _successor.Name);
                return await _successor.FallbackAsync(command, ct).ConfigureAwait(ContinueOnCapturedContext);
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
                .Where(method => method.Name == "HandleAsync")
                .SingleOrDefault(method => method.GetParameters().Count() == 2 
                    && method.GetParameters()[0].ParameterType == typeof(TRequest)
                    && method.GetParameters()[1].ParameterType == typeof(CancellationToken?));
        }

    }
}
