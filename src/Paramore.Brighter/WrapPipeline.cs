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

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// class WrapPipeline
    /// A pipeline with a source of a <see cref="TransformPipeline{TRequest}.MessageMapper"/> that:
    /// Takes a request and maps it to a message
    /// Runs transforms on that message
    /// </summary>
    public partial class WrapPipeline<TRequest> : TransformPipeline<TRequest> where TRequest: class, IRequest
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<WrapPipeline<TRequest>>();
            
        /// <summary>
        /// Constructs an instance of a wrap pipeline
        /// </summary>
        /// <param name="messageMapper">The message mapper that forms the pipeline source</param>
        /// <param name="messageTransformerFactory">Factory for transforms, required to release</param>
        /// <param name="transforms">The transforms applied after the message mapper</param>
        public WrapPipeline(
            IAmAMessageMapper<TRequest> messageMapper, 
            IAmAMessageTransformerFactory? messageTransformerFactory, 
            IEnumerable<IAmAMessageTransform> transforms
            ) : base(messageMapper, transforms)
        {
            if (messageTransformerFactory != null)
            {
                InstanceScope = new TransformLifetimeScope(messageTransformerFactory);
                Transforms.Each(transform => InstanceScope.Add(transform));
            }
        }

        /// <summary>        
        /// Lists the wrap pipeline: message mapper and filter transforms that will be executed
        /// Used for pipeline verification
        /// </summary>
        /// <param name="pipelineTracer"></param>
        public void DescribePath(TransformPipelineTracer pipelineTracer)
        {
            pipelineTracer.AddTransform(MessageMapper.GetType().Name);
            Transforms.Each(t => pipelineTracer.AddTransform(t.GetType().Name));
        }

        /// <summary>
        /// Transforms a <see cref="IRequest"/> into a <see cref="Message"/>
        /// Applies any required <see cref="IAmAMessageTransformAsync"/> to that <see cref="Message"/> 
        /// </summary>
        /// <param name="request">The request to wrap</param>
        /// <param name="requestContext">The context of the request in this pipeline</param>
        /// <param name="publication">The publication for this channel, provides metadata such as topic or Cloud Events attributes</param>
        /// <returns>The message created from the request via the pipeline</returns>
        public Message Wrap(TRequest request, RequestContext requestContext, Publication publication)
        {
            requestContext.Span ??= Activity.Current;

            MessageMapper.Context = requestContext;
            var message = MessageMapper.MapToMessage(request, publication);

            if (message.Header.Topic != publication.Topic)
            {
                Log.DifferentPublicationAndMessageTopic(s_logger, publication.Topic?.Value ?? string.Empty, message.Header.Topic);
            }

            BrighterTracer.WriteMapperEvent(message, publication, requestContext.Span, MessageMapper.GetType().Name, false, true);
            
            Transforms.Each(transform =>
            {
                transform.Context = requestContext;
                message = transform.Wrap(message, publication);
                BrighterTracer.WriteMapperEvent(message, publication, requestContext.Span, transform.GetType().Name, false);
            });

            if (!string.IsNullOrEmpty(publication.ReplyTo))
            {
                message.Header.ReplyTo = publication.ReplyTo!;
            } 
            
            return message;
        }
        
        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "Topic mismatch detected: The found topic ({FindPublicationTopic}) differs from the message topic ({MessageTopic}). This discrepancy could lead to invalid data in the pipeline")]
            public static partial void DifferentPublicationAndMessageTopic(ILogger logger, string findPublicationTopic, string messageTopic);
        }
    }
}
