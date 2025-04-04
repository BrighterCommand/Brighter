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
using System.Threading;
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
    public class TransformPipelineBuilderAsync
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TransformPipelineBuilder>();
        private readonly IAmAMessageMapperRegistryAsync _mapperRegistryAsync;

        private readonly IAmAMessageTransformerFactoryAsync _messageTransformerFactoryAsync;

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
        /// <param name="mapperRegistryAsync">The async message mapper registry, cannot be null</param>
        /// <param name="messageTransformerFactoryAsync">The async transform factory, can be null</param>
        /// <exception cref="ConfigurationException">Throws a configuration exception on a null mapperRegistry</exception>
        public TransformPipelineBuilderAsync(
            IAmAMessageMapperRegistryAsync mapperRegistryAsync, 
            IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync
            )
        {
            _mapperRegistryAsync = mapperRegistryAsync ?? throw new ConfigurationException("TransformPipelineBuilder expected a Message Mapper Registry but none supplied");
            _messageTransformerFactoryAsync = messageTransformerFactoryAsync;
        }

        /// <summary>
        /// Builds a pipeline.
        /// Anything marked with <see cref="WrapWithAttribute"/> will run before the <see cref="IAmAMessageMapper{TRequest}"/>
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public WrapPipelineAsync<TRequest> BuildWrapPipeline<TRequest>() where TRequest : class, IRequest
        {
            try
            {
                var messageMapper = FindMessageMapper<TRequest>();

                var transforms = BuildTransformPipeline<TRequest>(FindWrapTransforms(messageMapper));

                var pipeline = new WrapPipelineAsync<TRequest>(messageMapper, _messageTransformerFactoryAsync, transforms);

                s_logger.LogDebug("New wrap pipeline created for: {message} of {pipeline}", typeof(TRequest).Name, TraceWrapPipeline(pipeline));

                var unwraps = FindUnwrapTransforms(messageMapper);
                if (unwraps.Any())
                {
                    s_logger.LogDebug(
                        "Unwrap attributes on MapToMessage method for mapper of: {message} in {pipeline}, will be ignored", typeof(TRequest).Name,
                        TraceWrapPipeline(pipeline)
                    );
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
        public UnwrapPipelineAsync<TRequest> BuildUnwrapPipeline<TRequest>() where TRequest : class, IRequest
        {
            try
            {
                var messageMapper = FindMessageMapper<TRequest>();

                var transforms = BuildTransformPipeline<TRequest>(FindUnwrapTransforms(messageMapper));

                var pipeline = new UnwrapPipelineAsync<TRequest>(transforms, _messageTransformerFactoryAsync, messageMapper);

                s_logger.LogDebug(
                    "New unwrap pipeline created for: {message} of {pipeline}", typeof(TRequest).Name,
                    TraceUnwrapPipeline(pipeline)
                );
                
                var wraps = FindWrapTransforms(messageMapper);
                if (wraps.Any())
                {
                    s_logger.LogDebug(
                        "Wrap attributes on MapToRequest method for mapper of: {message} in {pipeline}, will be ignored", typeof(TRequest).Name,
                        TraceUnwrapPipeline(pipeline)
                    );
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
            return _mapperRegistryAsync.GetAsync<TRequest>() != null;
        }

        private IEnumerable<IAmAMessageTransformAsync> BuildTransformPipeline<TRequest>(IEnumerable<TransformAttribute> transformAttributes)
            where TRequest : class, IRequest
        {
            var transforms = new List<IAmAMessageTransformAsync>();

            //Allowed to be null to avoid breaking v9 interfaces
            if (_messageTransformerFactoryAsync == null)
            {
                int i = transformAttributes.Count();
                if (i > 0)
                    s_logger.LogWarning(
                        "No message transformer factory configured, so no transforms will be created but {transformCount} configured", i);

                return transforms;
            }

            transformAttributes.Each((attribute) =>
            {
                var transformType = attribute.GetHandlerType();
                var transformer = new TransformerFactoryAsync<TRequest>(attribute, _messageTransformerFactoryAsync).CreateMessageTransformer();
                if (transformer == null)
                {
                    throw new InvalidOperationException($"Message Transformer Factory could not create a transform of type {transformType.Name}");
                }

                transforms.Add(transformer);
            });

            return transforms;
        }

        public static void ClearPipelineCache()
        {
            s_wrapTransformsMemento.Clear();
            s_unWrapTransformsMemento.Clear();
        }

        private IAmAMessageMapperAsync<TRequest> FindMessageMapper<TRequest>() where TRequest : class, IRequest
        {
            var messageMapper = _mapperRegistryAsync.GetAsync<TRequest>();
            if (messageMapper == null) throw new InvalidOperationException($"Could not find mapper for {typeof(TRequest).Name}. Hint: did you set MessagePumpType.Proactor on the subscription to match the mapper type?");
            return messageMapper;
        }

        private IOrderedEnumerable<WrapWithAttribute> FindWrapTransforms<T>(IAmAMessageMapperAsync<T> messageMapper) where T : class, IRequest
        {
            var key = messageMapper.GetType().Name;
            return s_wrapTransformsMemento.GetOrAdd(key, s => FindMapToMessage(messageMapper)
                .GetOtherWrapsInPipeline()
                .OrderByDescending(attribute => attribute.Step));
        }

        private IOrderedEnumerable<UnwrapWithAttribute> FindUnwrapTransforms<T>(IAmAMessageMapperAsync<T> messageMapper) where T : class, IRequest
        {
            var key = messageMapper.GetType().Name;
            return s_unWrapTransformsMemento.GetOrAdd(key, s => FindMapToRequest(messageMapper)
                .GetOtherUnwrapsInPipeline()
                .OrderByDescending(attribute => attribute.Step));
        }

        private MethodInfo FindMapToMessage<TRequest>(IAmAMessageMapperAsync<TRequest> messageMapper) where TRequest : class, IRequest
        {
            return messageMapper.GetType().GetMethod(nameof(IAmAMessageMapperAsync<TRequest>.MapToMessageAsync),
                BindingFlags.Public | BindingFlags.Instance,
                null,
                CallingConventions.Any,
                new Type[] { typeof(TRequest), typeof(Publication), typeof(CancellationToken) },
                null)!;
        }


        private MethodInfo FindMapToRequest<TRequest>(IAmAMessageMapperAsync<TRequest> messageMapper) where TRequest : class, IRequest
        {
            return messageMapper.GetType().GetMethod(nameof(IAmAMessageMapperAsync<TRequest>.MapToRequestAsync),
                BindingFlags.Public | BindingFlags.Instance,
                null,
                CallingConventions.Any,
                new Type[] { typeof(Message), typeof(CancellationToken) },
                null)!;
        }

        private TransformPipelineTracer TraceWrapPipeline<TRequest>(WrapPipelineAsync<TRequest> pipeline) where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private TransformPipelineTracer TraceUnwrapPipeline<TRequest>(UnwrapPipelineAsync<TRequest> pipeline) where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}
