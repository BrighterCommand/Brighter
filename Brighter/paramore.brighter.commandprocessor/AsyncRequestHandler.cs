﻿// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
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

using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor
{
    public abstract class AsyncRequestHandler<TRequest> : IHandleRequestsAsync<TRequest> where TRequest : class, IRequest
    {
        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILog logger;
        private IHandleRequestsAsync<TRequest> _successor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRequestHandler{TRequest}"/> class.
        /// </summary>
        protected AsyncRequestHandler() 
            : this(LogProvider.GetCurrentClassLogger())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRequestHandler{TRequest}"/> class.
        /// Generally you can should prefer the default constructor, and we will grab the logger from your log provider rather than take a direct dependency.
        /// This can be helpful for testing.
        /// </summary>
        /// <param name="logger">The logger.</param>
        protected AsyncRequestHandler(ILog logger)
        {
            this.logger = logger;
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
        /// <returns>Awaitable <see cref="Task{TRequest}"/>.</returns>
        public virtual async Task<TRequest> HandleAsync(TRequest command)
        {
            if (_successor != null)
            {
                logger.DebugFormat("Passing request from {0} to {1}", Name, _successor.Name);
                return await _successor.HandleAsync(command);
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
        /// <returns>Awaitable <see cref="Task{TRequest}"/>.</returns>
        public virtual async Task<TRequest> FallbackAsync(TRequest command)
        {
            if (_successor != null)
            {
                logger.DebugFormat("Falling back from {0} to {1}", Name, _successor.Name);
                return await _successor.FallbackAsync(command);
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
                .Where(method => method.GetParameters().Count() == 1 && method.GetParameters().Single().ParameterType == typeof(TRequest))
                .SingleOrDefault();
        }

    }
}
