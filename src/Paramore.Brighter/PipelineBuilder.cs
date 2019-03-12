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
using System.Reflection;
using Paramore.Brighter.Inbox.Attributes;

namespace Paramore.Brighter
{
    public class PipelineBuilder<TRequest> : IAmAPipelineBuilder<TRequest>, IAmAnAsyncPipelineBuilder<TRequest>
        where TRequest : class, IRequest
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<PipelineBuilder<TRequest>>);

        private readonly IAmAHandlerFactory _handlerFactory;
        private readonly InboxConfiguration _inboxConfiguration;
        private readonly Interpreter<TRequest> _interpreter;
        private readonly IAmALifetime _instanceScope;
        private readonly IAmAHandlerFactoryAsync _asyncHandlerFactory;
        //GLOBAL! cache of handler attributes - won't change post-startup so avoid re-calculation. Method to clear cache below (if a broken test brought you here)
        private static readonly ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>> _preAttributesMemento = new ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>>();
        private static readonly ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>> _postAttributesMemento = new ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>>();

        /// <summary>
        /// Used to build a pipeline of handlers from the target handler and the attributes on that
        /// target handler which represent other filter steps in the pipeline
        /// </summary>
        /// <param name="registry">What handler services this request</param>
        /// <param name="handlerFactory">Callback to the user code to create instances of handlers</param>
        /// <param name="inboxConfiguration">Do we have a global attribute to add an inbox</param>
        public PipelineBuilder(
            IAmASubscriberRegistry registry, 
            IAmAHandlerFactory handlerFactory,
            InboxConfiguration inboxConfiguration = null) 
        {
            _handlerFactory = handlerFactory;
            _inboxConfiguration = inboxConfiguration;
            _instanceScope = new LifetimeScope(handlerFactory);
            _interpreter = new Interpreter<TRequest>(registry, handlerFactory);
        }

        public PipelineBuilder(
            IAmASubscriberRegistry registry, 
            IAmAHandlerFactoryAsync asyncHandlerFactory,
            InboxConfiguration inboxConfiguration = null)
        {
            _asyncHandlerFactory = asyncHandlerFactory;
            _inboxConfiguration = inboxConfiguration;
            _instanceScope = new LifetimeScope(asyncHandlerFactory);
            _interpreter = new Interpreter<TRequest>(registry, asyncHandlerFactory);
        }

        public Pipelines<TRequest> Build(IRequestContext requestContext)
        {
            var handlers = _interpreter.GetHandlers();

            var pipelines = new Pipelines<TRequest>();
            handlers.Each(handler => pipelines.Add(BuildPipeline(handler, requestContext)));

            pipelines.Each(handler => handler.AddToLifetime(_instanceScope));

            return pipelines;
        }

        /// <summary>
        /// Clears any cached pipeline definitions. Mainly intended as a helper for testing where the
        /// use of a static creates a shared fixture across tests (although in a production environment, shared
        /// is what we are after). If your pipeline tests don't respond as expected to manipulation, this may be your
        /// ally.
        /// </summary>
        public static void ClearPipelineCache()
        {
            _preAttributesMemento.Clear();
            _postAttributesMemento.Clear();
        }

        public void Dispose()
        {
            _instanceScope.Dispose();
        }

        private IHandleRequests<TRequest> BuildPipeline(RequestHandler<TRequest> implicitHandler, IRequestContext requestContext)
        {
            implicitHandler.Context = requestContext;

            if (!_preAttributesMemento.TryGetValue(implicitHandler.Name.ToString(), out IOrderedEnumerable<RequestHandlerAttribute> preAttributes))
            {
                preAttributes =
                    implicitHandler.FindHandlerMethod()
                        .GetOtherHandlersInPipeline()
                        .Where(attribute => attribute.Timing == HandlerTiming.Before)
                        .OrderByDescending(attribute => attribute.Step);

                AddGlobalInboxAttributes(ref preAttributes, implicitHandler);

                _preAttributesMemento.TryAdd(implicitHandler.Name.ToString(), preAttributes);

            }

            var firstInPipeline = PushOntoPipeline(preAttributes, implicitHandler, requestContext);


            if (!_postAttributesMemento.TryGetValue(implicitHandler.Name.ToString(), out IOrderedEnumerable<RequestHandlerAttribute> postAttributes))
            {
                postAttributes =
                    implicitHandler.FindHandlerMethod()
                        .GetOtherHandlersInPipeline()
                        .Where(attribute => attribute.Timing == HandlerTiming.After)
                        .OrderByDescending(attribute => attribute.Step);
            }

            AppendToPipeline(postAttributes, implicitHandler, requestContext);
            _logger.Value.DebugFormat("New handler pipeline created: {0}", TracePipeline(firstInPipeline));
            return firstInPipeline;
        }


        public AsyncPipelines<TRequest> BuildAsync(IRequestContext requestContext, bool continueOnCapturedContext)
        {
            var handlers = _interpreter.GetAsyncHandlers();

            var pipelines = new AsyncPipelines<TRequest>();
            handlers.Each(handler => pipelines.Add(BuildAsyncPipeline(handler, requestContext, continueOnCapturedContext)));

            pipelines.Each(handler => handler.AddToLifetime(_instanceScope));

            return pipelines;
        }

        private IHandleRequestsAsync<TRequest> BuildAsyncPipeline(RequestHandlerAsync<TRequest> implicitHandler, IRequestContext requestContext, bool continueOnCapturedContext)
        {
            implicitHandler.Context = requestContext;
            implicitHandler.ContinueOnCapturedContext = continueOnCapturedContext;

            if (!_preAttributesMemento.TryGetValue(implicitHandler.Name.ToString(), out IOrderedEnumerable<RequestHandlerAttribute> preAttributes))
            {
                preAttributes =
                    implicitHandler.FindHandlerMethod()
                        .GetOtherHandlersInPipeline()
                        .Where(attribute => attribute.Timing == HandlerTiming.Before)
                        .OrderByDescending(attribute => attribute.Step);

                AddGlobalInboxAttributesAsync(ref preAttributes, implicitHandler);

                _preAttributesMemento.TryAdd(implicitHandler.Name.ToString(), preAttributes);

            }


            AddGlobalInboxAttributesAsync(ref preAttributes, implicitHandler);
            
            var firstInPipeline = PushOntoAsyncPipeline(preAttributes, implicitHandler, requestContext, continueOnCapturedContext);

            if (!_postAttributesMemento.TryGetValue(implicitHandler.Name.ToString(), out IOrderedEnumerable<RequestHandlerAttribute> postAttributes))
            {
                postAttributes =
                    implicitHandler.FindHandlerMethod()
                        .GetOtherHandlersInPipeline()
                        .Where(attribute => attribute.Timing == HandlerTiming.After)
                        .OrderByDescending(attribute => attribute.Step);
            }

            AppendToAsyncPipeline(postAttributes, implicitHandler, requestContext);
            _logger.Value.DebugFormat("New async handler pipeline created: {0}", TracePipeline(firstInPipeline));
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

            var useInboxAttribute = new UseInboxAsyncAttribute(
                step: 0,
                contextKey: _inboxConfiguration.Context(implicitHandler.GetType()),
                onceOnly: _inboxConfiguration.OnceOnly,
                timing: HandlerTiming.Before,
                onceOnlyAction: _inboxConfiguration.ActionOnExists);

             PushOntoAttributeList(ref preAttributes, useInboxAttribute);
        }

        private void AppendToPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> implicitHandler, IRequestContext requestContext)
        {
            IHandleRequests<TRequest> lastInPipeline = implicitHandler;
            attributes.Each(attribute =>
            {
                var handlerType = attribute.GetHandlerType();
                if (handlerType.GetTypeInfo().GetInterfaces().Contains(typeof(IHandleRequests)))
                {
                    var decorator =
                        new HandlerFactory<TRequest>(attribute, _handlerFactory, requestContext).CreateRequestHandler();
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

        private void AppendToAsyncPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequestsAsync<TRequest> implicitHandler, IRequestContext requestContext)
        {
            IHandleRequestsAsync<TRequest> lastInPipeline = implicitHandler;
            attributes.Each(attribute =>
            {
                var handlerType = attribute.GetHandlerType();
                if (handlerType.GetTypeInfo().GetInterfaces().Contains(typeof(IHandleRequestsAsync)))
                {
                    var decorator = _asyncHandlerFactory.CreateAsyncRequestHandler<TRequest>(attribute, requestContext);
                    lastInPipeline.SetSuccessor(decorator);
                    lastInPipeline = decorator;
                }
                else
                {
                    var message = string.Format("All handlers in an async pipeline must derive from IHandleRequestsAsync. You cannot have a mixed pipeline by including handler {0}", handlerType.Name);
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
     
        private IHandleRequests<TRequest> PushOntoPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> lastInPipeline, IRequestContext requestContext)
        {
            attributes.Each(attribute =>
            {
                var handlerType = attribute.GetHandlerType();
                if (handlerType.GetTypeInfo().GetInterfaces().Contains(typeof(IHandleRequests)))
                {
                    var decorator =
                        new HandlerFactory<TRequest>(attribute, _handlerFactory, requestContext).CreateRequestHandler();
                    decorator.SetSuccessor(lastInPipeline);
                    lastInPipeline = decorator;
                }
                else
                {
                    var message = string.Format("All handlers in a pipeline must derive from IHandleRequests. You cannot have a mixed pipeline by including handler {0}", handlerType.Name);
                    throw new ConfigurationException(message);
                }
            });
            return lastInPipeline;
        }

        private IHandleRequestsAsync<TRequest> PushOntoAsyncPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequestsAsync<TRequest> lastInPipeline, IRequestContext requestContext, bool continueOnCapturedContext)
        {
            attributes.Each(attribute =>
            {
                var handlerType = attribute.GetHandlerType();
                if (handlerType.GetTypeInfo().GetInterfaces().Contains(typeof(IHandleRequestsAsync)))
                {
                    var decorator = _asyncHandlerFactory.CreateAsyncRequestHandler<TRequest>(attribute, requestContext);
                    decorator.ContinueOnCapturedContext = continueOnCapturedContext;
                    decorator.SetSuccessor(lastInPipeline);
                    lastInPipeline = decorator;
                }
                else
                {
                    var message = string.Format("All handlers in an async pipeline must derive from IHandleRequestsAsync. You cannot have a mixed pipeline by including handler {0}", handlerType.Name);
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
    }
}
