#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    /// <summary>
    /// We use attributes to allow you to mix in behaviours when message mapping such as offloading large payloads via a claim check or
    /// encrypting PII content. Because CSharp has single-inheritance, we can't build these in a re-usable fashion via inheritance, and
    /// instead of forcing you to use DI to pull in those behaviors, we offer the option to use an attribute to mark up your message transform
    /// with suitable changes.
    /// We run a <see cref="WrapWithAttribute"/> after the message mapper converts from an <see cref="IRequest"/>.
    /// We run a <see cref="UnwrapWithAttribute"/> before the message mapper converts to a <see cref="IRequest"/>.
    /// You handle translation between <see cref="IRequest"/> and <see cref="Message"/> in your <see cref="IAmAMessageMapper{TRequest}"/>
    /// </summary>
    public partial class TransformPipelineBuilder
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TransformPipelineBuilder>();
        private readonly IAmAMessageMapperRegistry _mapperRegistry;

        private readonly IAmAMessageTransformerFactory _messageTransformerFactory;

        //GLOBAL! Cache of message mapper transform attributes. This will not be recalculated post start up. Method to clear cache below (if a broken test brought you here).
        private static readonly ConcurrentDictionary<string, IOrderedEnumerable<WrapWithAttribute>> s_wrapTransformsMemento =
            new ConcurrentDictionary<string, IOrderedEnumerable<WrapWithAttribute>>();

        private static readonly ConcurrentDictionary<string, IOrderedEnumerable<UnwrapWithAttribute>> s_unWrapTransformsMemento =
            new ConcurrentDictionary<string, IOrderedEnumerable<UnwrapWithAttribute>>();

        /// <summary>
        /// Creates an instance of a transform pipeline builder.
        /// To avoid introducing a breaking interface for v9 we allow the transform factory to be optional,
        /// so that it can be optional in a CommandProcessor constructor, and not make a breaking change to the interface.
        /// In this case, transform pipelines mimic v9 behaviour and just run the mapper  and not any transforms
        /// To avoid silent failure, we warn on this.
        /// </summary>
        /// <param name="mapperRegistry">The message mapper registry, cannot be null</param>
        /// <param name="messageTransformerFactory">The transform factory, can be null</param>
        /// <exception cref="ConfigurationException">Throws a configuration exception on a null mapperRegistry</exception>
        public TransformPipelineBuilder(
            IAmAMessageMapperRegistry mapperRegistry, 
            IAmAMessageTransformerFactory messageTransformerFactory
            )
        {
            _mapperRegistry = mapperRegistry ??
                              throw new ConfigurationException("TransformPipelineBuilder expected a Message Mapper Registry but none supplied");

            _messageTransformerFactory = messageTransformerFactory;
        }

        /// <summary>
        /// Builds a pipeline.
        /// Anything marked with <see cref="WrapWithAttribute"/> will run before the <see cref="IAmAMessageMapper{TRequest}"/>
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public WrapPipeline<TRequest> BuildWrapPipeline<TRequest>() where TRequest : class, IRequest
        {
            try
            {
                var messageMapper = FindMessageMapper<TRequest>();

                var transforms = BuildTransformPipeline<TRequest>(FindWrapTransforms(messageMapper));

                var pipeline = new WrapPipeline<TRequest>(messageMapper, _messageTransformerFactory, transforms);

                Log.NewWrapPipelineCreated(s_logger, typeof(TRequest).Name, TraceWrapPipeline(pipeline));

                var unwraps = FindUnwrapTransforms(messageMapper);
                if (unwraps.Any())
                {
                    Log.UnwrapAttributesOnMapToMessageMethodIgnored(s_logger, typeof(TRequest).Name, TraceWrapPipeline(pipeline));
                }

                return pipeline;
            }
            catch (Exception e)
            {
                throw new ConfigurationException("Error building wrap pipeline for outgoing message, see inner exception for details", e);
            }
        }

        /// <summary>
        /// Builds a pipeline.
        /// Anything marked with <see cref="UnwrapWithAttribute"/> will run after the <see cref="IAmAMessageMapper{TRequest}"/>
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public UnwrapPipeline<TRequest> BuildUnwrapPipeline<TRequest>() where TRequest : class, IRequest
        {
            try
            {
                var messageMapper = FindMessageMapper<TRequest>();

                var transforms = BuildTransformPipeline<TRequest>(FindUnwrapTransforms(messageMapper));

                var pipeline = new UnwrapPipeline<TRequest>(transforms, _messageTransformerFactory, messageMapper);

                Log.NewUnwrapPipelineCreated(s_logger, typeof(TRequest).Name, TraceUnwrapPipeline(pipeline));

                var wraps = FindWrapTransforms(messageMapper);
                if (wraps.Any())
                {
                    Log.WrapAttributesOnMapToRequestMethodIgnored(s_logger, typeof(TRequest).Name, TraceUnwrapPipeline(pipeline));
                }

                return pipeline;
            }
            catch (Exception e)
            {
                throw new ConfigurationException("Error building unwrap pipeline for outgoing message, see inner exception for details", e);
            }
        }

        public bool HasPipeline<TRequest>() where TRequest : class, IRequest
        {
            return _mapperRegistry.Get<TRequest>() != null;
        }

        private IEnumerable<IAmAMessageTransform> BuildTransformPipeline<TRequest>(IEnumerable<TransformAttribute> transformAttributes)
            where TRequest : class, IRequest
        {
            var transforms = new List<IAmAMessageTransform>();

            //Allowed to be null to avoid breaking v9 interfaces
            if (_messageTransformerFactory is null)
            {
                int i = transformAttributes.Count();
                if (i > 0)
                    Log.NoMessageTransformerFactoryConfigured(s_logger, i);

                return transforms;
            }

            transformAttributes.Each((attribute) =>
            {
                var transformType = attribute.GetHandlerType();
                var transformer = new TransformerFactory<TRequest>(attribute, _messageTransformerFactory).CreateMessageTransformer();
                if (transformer is null)
                {
                    throw new InvalidOperationException(string.Format("Message Transformer Factory could not create a transform of type {0}",
                        transformType.Name));
                }
                else
                {
                    transforms.Add(transformer);
                }
            });

            return transforms;
        }

        public static void ClearPipelineCache()
        {
            s_wrapTransformsMemento.Clear();
            s_unWrapTransformsMemento.Clear();
        }

        private IAmAMessageMapper<TRequest> FindMessageMapper<TRequest>() where TRequest : class, IRequest
        {
            var messageMapper = _mapperRegistry.Get<TRequest>();
            if (messageMapper == null) throw new InvalidOperationException(string.Format("Could not find mapper for {0}. Hint: did you set runAsync on the subscription to match the mapper type?", typeof(TRequest).Name));
            return messageMapper;
        }

        private IOrderedEnumerable<WrapWithAttribute> FindWrapTransforms<T>(IAmAMessageMapper<T> messageMapper) where T : class, IRequest
        {
            var key = messageMapper.GetType().Name;
            if (!s_wrapTransformsMemento.TryGetValue(key, out IOrderedEnumerable<WrapWithAttribute>? transformAttributes))
            {
                transformAttributes = FindMapToMessage(messageMapper)
                    .GetOtherWrapsInPipeline()
                    .OrderByDescending(attribute => attribute.Step);

                s_wrapTransformsMemento.TryAdd(key, transformAttributes);
            }

            return transformAttributes;
        }

        private IOrderedEnumerable<UnwrapWithAttribute> FindUnwrapTransforms<T>(IAmAMessageMapper<T> messageMapper) where T : class, IRequest
        {
            var key = messageMapper.GetType().Name;
            if (!s_unWrapTransformsMemento.TryGetValue(key, out IOrderedEnumerable<UnwrapWithAttribute>? transformAttributes))
            {
                transformAttributes = FindMapToRequest(messageMapper)
                    .GetOtherUnwrapsInPipeline()
                    .OrderByDescending(attribute => attribute.Step);

                s_unWrapTransformsMemento.TryAdd(key, transformAttributes);
            }

            return transformAttributes;
        }

        private MethodInfo FindMapToMessage<TRequest>(IAmAMessageMapper<TRequest> messageMapper) where TRequest : class, IRequest
        {
            return FindMethods(messageMapper)
                .Where(method => method.Name == nameof(IAmAMessageMapper<TRequest>.MapToMessage))
                .SingleOrDefault(
                    method => method.GetParameters().Length == 2 
                    && method.GetParameters().First().ParameterType == typeof(TRequest)
                    && method.GetParameters().Last().ParameterType == typeof(Publication)
                )!;
        }

        private MethodInfo FindMapToRequest<TRequest>(IAmAMessageMapper<TRequest> messageMapper) where TRequest : class, IRequest
        {
            return FindMethods(messageMapper)
                .Where(method => method.Name == nameof(IAmAMessageMapper<TRequest>.MapToRequest))
                .SingleOrDefault(
                    method => method.GetParameters().Length == 1 
                    && method.GetParameters().Single().ParameterType == typeof(Message)
                )!;
        }

        private static MethodInfo[] FindMethods<TRequest>(IAmAMessageMapper<TRequest> messageMapper) where TRequest : class, IRequest
        {
            return messageMapper.GetType().GetMethods();
        }

        private TransformPipelineTracer TraceWrapPipeline<TRequest>(WrapPipeline<TRequest> pipeline) where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private TransformPipelineTracer TraceUnwrapPipeline<TRequest>(UnwrapPipeline<TRequest> pipeline) where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
        
        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "New wrap pipeline created for: {Message} of {Pipeline}")]
            public static partial void NewWrapPipelineCreated(ILogger logger, string message, TransformPipelineTracer pipeline);

            [LoggerMessage(LogLevel.Debug, "Unwrap attributes on MapToMessage method for mapper of: {Message} in {Pipeline}, will be ignored")]
            public static partial void UnwrapAttributesOnMapToMessageMethodIgnored(ILogger logger, string message, TransformPipelineTracer pipeline);
            
            [LoggerMessage(LogLevel.Debug, "New unwrap pipeline created for: {Message} of {Pipeline}")]
            public static partial void NewUnwrapPipelineCreated(ILogger logger, string message, TransformPipelineTracer pipeline);

            [LoggerMessage(LogLevel.Debug, "Wrap attributes on MapToRequest method for mapper of: {Message} in {Pipeline}, will be ignored")]
            public static partial void WrapAttributesOnMapToRequestMethodIgnored(ILogger logger, string message, TransformPipelineTracer pipeline);

            [LoggerMessage(LogLevel.Warning, "No message transformer factory configured, so no transforms will be created but {TransformCount} configured")]
            public static partial void NoMessageTransformerFactoryConfigured(ILogger logger, int transformCount);
        }
    }
}

