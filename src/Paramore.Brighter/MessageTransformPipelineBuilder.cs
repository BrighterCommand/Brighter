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
using Paramore.Brighter.Extensions;

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
    public class MessageTransformPipelineBuilder
    {
        private readonly MessageMapperRegistry _mapperRegistry;
        private readonly IAmAMessageTransformerFactory _messageTransformerFactory;

        public MessageTransformPipelineBuilder(MessageMapperRegistry mapperRegistry, IAmAMessageTransformerFactory messageTransformerFactory)
        {
            _mapperRegistry = mapperRegistry;
            _messageTransformerFactory = messageTransformerFactory;
        }

        /// <summary>
        /// Builds a pipeline.
        /// Anything marked with <see cref=""/> will run before the <see cref="IAmAMessageMapper{TRequest}"/>
        /// Anything marked with
        /// </summary>
        /// <param name="message"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <returns></returns>
        public WrapPipeline<TRequest> BuildWrapPipeline<TRequest>(TRequest message) where TRequest : class, IRequest, new()
        {
            var messageMapper = _mapperRegistry.Get<TRequest>();

            var transforms = BuildTransformPipeline<TRequest>(FindWrapTransforms(messageMapper));

            return new WrapPipeline<TRequest>(messageMapper, transforms);
        }

         public UnwrapPipeline<TRequest> BuildUnwrapPipeline<TRequest>(TRequest request) where TRequest : class, IRequest, new()
         {
             var messageMapper = _mapperRegistry.Get<TRequest>();

             var transforms = BuildTransformPipeline<TRequest>(FindUnwrapTransforms(messageMapper));

             return new UnwrapPipeline<TRequest>(transforms, messageMapper);
         }
         
         private IEnumerable<IAmAMessageTransformAsync> BuildTransformPipeline<TRequest>(IEnumerable<TransformAttribute> transformAttributes)
            where TRequest: class, IRequest, new() 
        {
            var transforms = new List<IAmAMessageTransformAsync>();
            transformAttributes.Each((attribute) =>
            {
                var transformType = attribute.GetHandlerType();
                var transformer = new TransformerFactory<TRequest>(attribute, _messageTransformerFactory).CreateMessageTransformer();
                transforms.Add(transformer);
            });

            return transforms;
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
    }
}
