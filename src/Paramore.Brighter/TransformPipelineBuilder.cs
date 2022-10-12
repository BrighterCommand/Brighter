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
    public class TransformPipelineBuilder : IDisposable
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TransformPipelineBuilder>();
        private readonly MessageMapperRegistry _mapperRegistry;
        private readonly IAmAMessageTransformerFactory _messageTransformerFactory;
        private readonly TransformLifetimeScope _instanceScope;

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
        public TransformPipelineBuilder(MessageMapperRegistry mapperRegistry, IAmAMessageTransformerFactory messageTransformerFactory)
        {
            _mapperRegistry = mapperRegistry ?? throw new ConfigurationException("TransformPipelineBuilder expected a Message Mapper Registry but none supplied");

            _messageTransformerFactory = messageTransformerFactory;

            _instanceScope = new TransformLifetimeScope(messageTransformerFactory);
        }

        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for any transforms generated for the pipeline 
        /// </summary>
        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }
  
        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for any transforms generated for the pipeline 
        /// </summary>
        ~TransformPipelineBuilder()
        {
            ReleaseUnmanagedResources();
        }      
          
          /// <summary>
        /// Builds a pipeline.
        /// Anything marked with <see cref=""/> will run before the <see cref="IAmAMessageMapper{TRequest}"/>
        /// Anything marked with
        /// </summary>
        /// <param name="request"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <returns></returns>
        public WrapPipeline<TRequest> BuildWrapPipeline<TRequest>(TRequest request) where TRequest : class, IRequest, new()
        {
            try
            {
                var messageMapper = FindMessageMapper<TRequest>();

                var transforms = BuildTransformPipeline<TRequest>(FindWrapTransforms(messageMapper));

                var pipeline = new WrapPipeline<TRequest>(messageMapper, transforms);
                
                s_logger.LogDebug(
                    "New wrap pipeline created for: {message} of {pipeline}", nameof(request), 
                    TraceWrapPipeline(pipeline)
                    );

                return pipeline;
            }
            catch (Exception e)
            {
                throw new ConfigurationException("Error building wrap pipeline for outgoing message, see inner exception for details", e);
            }
        }

        public UnwrapPipeline<TRequest> BuildUnwrapPipeline<TRequest>(TRequest request) where TRequest : class, IRequest, new()
        {
            try
            {
                var messageMapper = FindMessageMapper<TRequest>();

                var transforms = BuildTransformPipeline<TRequest>(FindUnwrapTransforms(messageMapper));

                var pipeline = new UnwrapPipeline<TRequest>(transforms, messageMapper);
                
                s_logger.LogDebug(
                    "New unwrap pipeline created for: {message} of {pipeline}", nameof(request), 
                    TraceUnwrapPipeline(pipeline)
                    );
                
                return pipeline;
            }
            catch (Exception e)
            {
                throw new ConfigurationException("Error building unwrap pipeline for outgoing message, see inner exception for details", e);
            }
        }

        private IEnumerable<IAmAMessageTransformAsync> BuildTransformPipeline<TRequest>(IEnumerable<TransformAttribute> transformAttributes)
            where TRequest : class, IRequest, new()
        {
            var transforms = new List<IAmAMessageTransformAsync>();
            
            //Allowed to be null to avoid breaking v9 interfaces
            if (_messageTransformerFactory == null)
            {
                int i = transformAttributes.Count();
                if (i >= 0)
                s_logger.LogWarning(
                    "No message transfomer factory configured, so no transforms will be created but {transformCount} configured",i);
                
                return transforms;
            }

            transformAttributes.Each((attribute) =>
            {
                var transformType = attribute.GetHandlerType();
                var transformer = new TransformerFactory<TRequest>(attribute, _messageTransformerFactory).CreateMessageTransformer();
                if (transformer == null)
                {
                    throw new InvalidOperationException(string.Format("Message Transformer Factory could not create a transform of type {0}", transformType.Name));
                }
                else
                {
                    transforms.Add(transformer);
                }
            });

            return transforms;
        }

        private IAmAMessageMapper<TRequest> FindMessageMapper<TRequest>() where TRequest : class, IRequest, new()
        {
            var messageMapper = _mapperRegistry.Get<TRequest>();
            if (messageMapper == null) throw new InvalidOperationException(string.Format("Could not find mapper for {0}", typeof(TRequest).Name));
            return messageMapper;
        }

        private IOrderedEnumerable<WrapWithAttribute> FindWrapTransforms<T>(IAmAMessageMapper<T> messageMapper) where T : class, IRequest, new()
        {
            return FindMapToMessage(messageMapper)
                .GetOtherWrapsInPipeline()
                .OrderByDescending(attribute => attribute.Step);
        }

        private IOrderedEnumerable<UnwrapWithAttribute> FindUnwrapTransforms<T>(IAmAMessageMapper<T> messageMapper) where T : class, IRequest, new()
        {
            return FindMapToRequest(messageMapper)
                .GetOtherUnwrapsInPipeline()
                .OrderByDescending(attribute => attribute.Step);
        }


        private MethodInfo FindMapToMessage<TRequest>(IAmAMessageMapper<TRequest> messageMapper) where TRequest : class, IRequest, new()
        {
            return FindMethods(messageMapper)
                .Where(method => method.Name == nameof(IAmAMessageMapper<TRequest>.MapToMessage))
                .SingleOrDefault(method => method.GetParameters().Length == 1 && method.GetParameters().Single().ParameterType == typeof(TRequest));
        }


        private MethodInfo FindMapToRequest<TRequest>(IAmAMessageMapper<TRequest> messageMapper) where TRequest : class, IRequest, new()
        {
            return FindMethods(messageMapper)
                .Where(method => method.Name == nameof(IAmAMessageMapper<TRequest>.MapToRequest))
                .SingleOrDefault(method => method.GetParameters().Length == 1 && method.GetParameters().Single().ParameterType == typeof(Message));
        }

        private static MethodInfo[] FindMethods<TRequest>(IAmAMessageMapper<TRequest> messageMapper) where TRequest : class, IRequest, new()
        {
            return messageMapper.GetType().GetTypeInfo().GetMethods();
        }
        
        private TransformPipelineTracer TraceWrapPipeline<TRequest>(WrapPipeline<TRequest> pipeline) where TRequest : class, IRequest, new()
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
        
        private TransformPipelineTracer TraceUnwrapPipeline<TRequest>(UnwrapPipeline<TRequest> pipeline) where TRequest : class, IRequest, new()
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private void ReleaseUnmanagedResources()
        {
            _instanceScope.Dispose();
        }


    }
}
