#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Inbox.Attributes;

namespace Paramore.Brighter
{
    public class PipelineBuilder<TRequest> : IAmAPipelineBuilder<TRequest>, IAmAnAsyncPipelineBuilder<TRequest>
        where TRequest : class, IRequest
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<PipelineBuilder<TRequest>>();

        private readonly IAmASubscriberRegistry _subscriberRegistry;
        private readonly IAmAHandlerFactorySync? _handlerFactorySync;
        private readonly InboxConfiguration? _inboxConfiguration;
        private readonly IAmAHandlerFactoryAsync? _asyncHandlerFactory;
        private readonly List<IAmALifetime> _instanceScopes = new List<IAmALifetime>();
        //GLOBAL! cache of handler attributes - won't change post-startup so avoid re-calculation. Method to clear cache below (if a broken test brought you here)
        private static readonly ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>> s_preAttributesMemento = new ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>>();
        private static readonly ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>> s_postAttributesMemento = new ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>>();

        /// <summary>
        /// Used to build a pipeline of handlers from the target handler and the attributes on that
        /// target handler which represent other filter steps in the pipeline
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry</param>
        /// <param name="handlerFactorySync">Callback to the user code to create instances of handlers</param>
        /// <param name="inboxConfiguration">Do we have a global attribute to add an inbox</param>
        public PipelineBuilder(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactorySync handlerFactorySync,
            InboxConfiguration? inboxConfiguration = null) 
        {
            _subscriberRegistry = subscriberRegistry;
            _handlerFactorySync = handlerFactorySync;
            _inboxConfiguration = inboxConfiguration;
        }

        public PipelineBuilder(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactoryAsync asyncHandlerFactory,
            InboxConfiguration? inboxConfiguration = null)
        {
            _subscriberRegistry = subscriberRegistry;
            _asyncHandlerFactory = asyncHandlerFactory;
            _inboxConfiguration = inboxConfiguration;
        }
        
        public Pipelines<TRequest> Build(IRequestContext requestContext)
        {
            if(_handlerFactorySync is null)
                throw new NullReferenceException("HandlerFactorySync is null");
            
            try
            {
                var observers = _subscriberRegistry.Get<TRequest>();
                
                var pipelines = new Pipelines<TRequest>();
                
                observers.Each(observer =>
                {
                    var context = observers.Count() == 1 ? requestContext : requestContext.CreateCopy();
                    var instanceScope = GetSyncInstanceScope();
                    var handler = (RequestHandler<TRequest>)_handlerFactorySync.Create(observer, instanceScope);
                    var pipeline = BuildPipeline(handler, context, instanceScope);
                    pipeline.AddToLifetime(instanceScope);
                    
                    pipelines.Add(pipeline);
                });

                return pipelines;
            }
            catch (Exception e) when (e is not ConfigurationException)
            {
                throw new ConfigurationException("Error when building pipeline, see inner Exception for details", e);
            }
        }

        /// <summary>
        /// Clears any cached pipeline definitions. Mainly intended as a helper for testing where the
        /// use of a static creates a shared fixture across tests (although in a production environment, shared
        /// is what we are after). If your pipeline tests don't respond as expected to manipulation, this may be your
        /// ally.
        /// </summary>
        public static void ClearPipelineCache()
        {
            s_preAttributesMemento.Clear();
            s_postAttributesMemento.Clear();
        }

        public void Dispose()
            => _instanceScopes.Each(s => s.Dispose());

        private IHandleRequests<TRequest> BuildPipeline(RequestHandler<TRequest> implicitHandler,
            IRequestContext requestContext, IAmALifetime instanceScope)
        {
            if (implicitHandler is null)
            {
                throw new ArgumentNullException(nameof(implicitHandler));
            }

            implicitHandler.Context = requestContext;

            if (!s_preAttributesMemento.TryGetValue(implicitHandler.Name.ToString(),
                    out IOrderedEnumerable<RequestHandlerAttribute>? preAttributes))
            {
                preAttributes =
                    implicitHandler.FindHandlerMethod()
                        .GetOtherHandlersInPipeline()
                        .Where(attribute => attribute.Timing == HandlerTiming.Before)
                        .OrderByDescending(attribute => attribute.Step);

                AddGlobalInboxAttributes(ref preAttributes, implicitHandler);

                s_preAttributesMemento.TryAdd(implicitHandler.Name.ToString(), preAttributes);

            }

            var firstInPipeline = PushOntoPipeline(preAttributes, implicitHandler, requestContext, instanceScope);


            if (!s_postAttributesMemento.TryGetValue(implicitHandler.Name.ToString(),
                    out IOrderedEnumerable<RequestHandlerAttribute>? postAttributes))
            {
                postAttributes =
                    implicitHandler.FindHandlerMethod()
                        .GetOtherHandlersInPipeline()
                        .Where(attribute => attribute.Timing == HandlerTiming.After)
                        .OrderByDescending(attribute => attribute.Step);
            }

            AppendToPipeline(postAttributes, implicitHandler, requestContext, instanceScope);
            s_logger.LogDebug("New handler pipeline created: {HandlerName}", TracePipeline(firstInPipeline));
            return firstInPipeline;
        }


        public AsyncPipelines<TRequest> BuildAsync(IRequestContext requestContext, bool continueOnCapturedContext)
        {
            if(_asyncHandlerFactory is null)
                throw new NullReferenceException("AsyncHandlerFactory is null");
            
            try
            {
                
                var observers = _subscriberRegistry.Get<TRequest>();

                var pipelines = new AsyncPipelines<TRequest>();
                
                observers.Each(observer =>
                {
                    var context = observers.Count() == 1 ? requestContext : requestContext.CreateCopy();
                    var instanceScope = GetAsyncInstanceScope();
                    var handler = (RequestHandlerAsync<TRequest>)_asyncHandlerFactory.Create(observer, instanceScope);
                    var pipeline = BuildAsyncPipeline(handler, context, instanceScope,
                        continueOnCapturedContext);
                    pipeline.AddToLifetime(instanceScope);
                    
                    pipelines.Add(pipeline);
                });

                return pipelines;
            }
            catch (Exception e) when(!(e is ConfigurationException))
            {
                throw new ConfigurationException("Error when building pipeline, see inner Exception for details", e);
            }
        }

        private IHandleRequestsAsync<TRequest> BuildAsyncPipeline(RequestHandlerAsync<TRequest> implicitHandler, IRequestContext requestContext, IAmALifetime instanceScope, bool continueOnCapturedContext)
        {
            if (implicitHandler is null)
            {
                throw new ArgumentNullException(nameof(implicitHandler));
            }

            implicitHandler.Context = requestContext;
            implicitHandler.ContinueOnCapturedContext = continueOnCapturedContext;

            if (!s_preAttributesMemento.TryGetValue(implicitHandler.Name.ToString(), out IOrderedEnumerable<RequestHandlerAttribute>? preAttributes))
            {
                preAttributes =
                    implicitHandler.FindHandlerMethod()
                        .GetOtherHandlersInPipeline()
                        .Where(attribute => attribute.Timing == HandlerTiming.Before)
                        .OrderByDescending(attribute => attribute.Step);

                AddGlobalInboxAttributesAsync(ref preAttributes, implicitHandler);

                s_preAttributesMemento.TryAdd(implicitHandler.Name.ToString(), preAttributes);

            }


            AddGlobalInboxAttributesAsync(ref preAttributes, implicitHandler);
            
            var firstInPipeline = PushOntoAsyncPipeline(preAttributes, implicitHandler, requestContext, instanceScope, continueOnCapturedContext);

            if (!s_postAttributesMemento.TryGetValue(implicitHandler.Name.ToString(), out IOrderedEnumerable<RequestHandlerAttribute>? postAttributes))
            {
                postAttributes =
                    implicitHandler.FindHandlerMethod()
                        .GetOtherHandlersInPipeline()
                        .Where(attribute => attribute.Timing == HandlerTiming.After)
                        .OrderByDescending(attribute => attribute.Step);
            }

            AppendToAsyncPipeline(postAttributes, implicitHandler, requestContext, instanceScope);
            s_logger.LogDebug("New async handler pipeline created: {HandlerName}", TracePipeline(firstInPipeline));
            return firstInPipeline;
        }

        private void AddGlobalInboxAttributes(ref IOrderedEnumerable<RequestHandlerAttribute> preAttributes, RequestHandler<TRequest> implicitHandler)
        {
            if (
                _inboxConfiguration == null 
                || implicitHandler.FindHandlerMethod().HasNoInboxAttributesInPipeline()
                || implicitHandler.FindHandlerMethod().HasExistingUseInboxAttributesInPipeline()
            )
                return;

            if (_inboxConfiguration is null)
                throw new ArgumentException("Inbox Configuration must be provided");
            if (_inboxConfiguration.Context is null)
                throw new ArgumentException("Inbox Configuration must be set");
            var useInboxAttribute = new UseInboxAttribute(
                step: 0,
                contextKey: _inboxConfiguration.Context(implicitHandler.GetType()),
                onceOnly: _inboxConfiguration.OnceOnly,
                timing: HandlerTiming.Before,
                onceOnlyAction: _inboxConfiguration.ActionOnExists);
            
             PushOntoAttributeList(ref preAttributes, useInboxAttribute);
        }


        private void AddGlobalInboxAttributesAsync(ref IOrderedEnumerable<RequestHandlerAttribute> preAttributes, RequestHandlerAsync<TRequest> implicitHandler)
        {
            if (_inboxConfiguration == null 
                || implicitHandler.FindHandlerMethod().HasNoInboxAttributesInPipeline()
                || implicitHandler.FindHandlerMethod().HasExistingUseInboxAttributesInPipeline()
     
            )
                return;

            if (_inboxConfiguration is null)
                throw new ArgumentException("Inbox Configuration must be provided");
            if (_inboxConfiguration.Context is null)
                throw new ArgumentException("Inbox Configuration must be set");
            var useInboxAttribute = new UseInboxAsyncAttribute(
                step: 0,
                contextKey: _inboxConfiguration.Context(implicitHandler.GetType()),
                onceOnly: _inboxConfiguration.OnceOnly,
                timing: HandlerTiming.Before,
                onceOnlyAction: _inboxConfiguration.ActionOnExists);

             PushOntoAttributeList(ref preAttributes, useInboxAttribute);
        }

        private void AppendToPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> implicitHandler, IRequestContext requestContext, IAmALifetime instanceScope)
        {
            IHandleRequests<TRequest> lastInPipeline = implicitHandler;
            attributes.Each(attribute =>
            {
                var handlerType = attribute.GetHandlerType();
                if (handlerType.GetInterfaces().Contains(typeof(IHandleRequests)))
                {
                    var decorator =
                        new HandlerFactory<TRequest>(attribute, _handlerFactorySync!, requestContext).CreateRequestHandler(instanceScope);
                    lastInPipeline.SetSuccessor(decorator);
                    lastInPipeline = decorator;
                }
                else
                {
                    var message = string.Format("All handlers in a pipeline must derive from IHandleRequests. You cannot have a mixed pipeline by including handler {0}", handlerType.Name);
                    throw new ConfigurationException(message);
                }
            });
        }

        private void AppendToAsyncPipeline(IEnumerable<RequestHandlerAttribute> attributes,
            IHandleRequestsAsync<TRequest> implicitHandler, IRequestContext requestContext, IAmALifetime instanceScope)
        {
            IHandleRequestsAsync<TRequest> lastInPipeline = implicitHandler;
            attributes.Each(attribute =>
            {
                var handlerType = attribute.GetHandlerType();
                if (handlerType.GetInterfaces().Contains(typeof(IHandleRequestsAsync)))
                {
                    var decorator =
                        _asyncHandlerFactory!.CreateAsyncRequestHandler<TRequest>(attribute, requestContext,
                            instanceScope);
                    lastInPipeline.SetSuccessor(decorator);
                    lastInPipeline = decorator;
                }
                else
                {
                    var message =
                        string.Format(
                            "All handlers in an async pipeline must derive from IHandleRequestsAsync. You cannot have a mixed pipeline by including handler {0}",
                            handlerType.Name);
                    throw new ConfigurationException(message);
                }
            });
        }

        private static void PushOntoAttributeList(ref IOrderedEnumerable<RequestHandlerAttribute> preAttributes, RequestHandlerAttribute requestHandlerAttribute)
        {
            var attributeList = new List<RequestHandlerAttribute>();

            attributeList.Add(requestHandlerAttribute);

            preAttributes.Each(handler =>
            {
                handler.Step++;
                attributeList.Add(handler);
            });

            preAttributes = attributeList.OrderByDescending(handler => handler.Step);
        }

        private IHandleRequests<TRequest> PushOntoPipeline(IEnumerable<RequestHandlerAttribute> attributes,
            IHandleRequests<TRequest> lastInPipeline, IRequestContext requestContext, IAmALifetime instanceScope)
        {
            attributes.Each(attribute =>
            {
                var handlerType = attribute.GetHandlerType();
                if (handlerType.GetInterfaces().Contains(typeof(IHandleRequests)))
                {
                    var decorator =
                        new HandlerFactory<TRequest>(attribute, _handlerFactorySync!, requestContext)
                            .CreateRequestHandler(instanceScope);
                    decorator.SetSuccessor(lastInPipeline);
                    lastInPipeline = decorator;
                }
                else
                {
                    var message =
                        string.Format(
                            "All handlers in a pipeline must derive from IHandleRequests. You cannot have a mixed pipeline by including handler {0}",
                            handlerType.Name);
                    throw new ConfigurationException(message);
                }
            });
            return lastInPipeline;
        }

        private IHandleRequestsAsync<TRequest> PushOntoAsyncPipeline(IEnumerable<RequestHandlerAttribute> attributes,
            IHandleRequestsAsync<TRequest> lastInPipeline, IRequestContext requestContext, IAmALifetime instanceScope,
            bool continueOnCapturedContext)
        {
            attributes.Each(attribute =>
            {
                var handlerType = attribute.GetHandlerType();
                if (handlerType.GetInterfaces().Contains(typeof(IHandleRequestsAsync)))
                {
                    var decorator =
                        _asyncHandlerFactory!.CreateAsyncRequestHandler<TRequest>(attribute, requestContext,
                            instanceScope);
                    decorator.ContinueOnCapturedContext = continueOnCapturedContext;
                    decorator.SetSuccessor(lastInPipeline);
                    lastInPipeline = decorator;
                }
                else
                {
                    var message =
                        string.Format(
                            "All handlers in an async pipeline must derive from IHandleRequestsAsync. You cannot have a mixed pipeline by including handler {0}",
                            handlerType.Name);
                    throw new ConfigurationException(message);
                }
            });
            return lastInPipeline;
        }

        private PipelineTracer TracePipeline(IHandleRequests<TRequest> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private PipelineTracer TracePipeline(IHandleRequestsAsync<TRequest> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private IAmALifetime GetSyncInstanceScope()
        {
            if(_handlerFactorySync is null)
                throw new NullReferenceException("HandlerFactorySync is null");

            var scope = new HandlerLifetimeScope(_handlerFactorySync);
            _instanceScopes.Add(scope);
            
            return scope;
        }

        private IAmALifetime GetAsyncInstanceScope()
        {
            if(_asyncHandlerFactory is null)
                throw new NullReferenceException("AsyncHandlerFactory is null");
            
            var scope = new HandlerLifetimeScope(_asyncHandlerFactory);
            _instanceScopes.Add(scope);
            
            return scope;
        }
    }
}
