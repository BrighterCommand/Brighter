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
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter
{
    /// <summary>
    /// class WrapPipeline
    /// A pipeline with a source of a <see cref="MessageMapper"/> that:
    /// Takes a request and maps it to a message
    /// Runs transforms on that message
    /// </summary>
    public class WrapPipelineAsync<TRequest> : TransformPipelineAsync<TRequest> where TRequest: class, IRequest
    {
        /// <summary>
        /// Constructs an instance of a wrap pipeline
        /// </summary>
        /// <param name="messageMapperAsync">The message mapper that forms the pipeline source</param>
        /// <param name="messageTransformerFactoryAsync">Factory for transforms, required to release</param>
        /// <param name="transforms">The transforms applied after the message mapper</param>
        public WrapPipelineAsync(
            IAmAMessageMapperAsync<TRequest> messageMapperAsync, 
            IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync, 
            IEnumerable<IAmAMessageTransformAsync> transforms
            )
        {
            MessageMapper = messageMapperAsync;
            Transforms = transforms;
            if (messageTransformerFactoryAsync != null)
            {
                InstanceScope = new TransformLifetimeScopeAsync(messageTransformerFactoryAsync);
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
        /// <param name="publication">The publication for this channel, provides metadata such as topic or Cloud Events attributes</param>
        /// <param name="requestContext">The context of the request in this pipeline</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        public async Task<Message> WrapAsync(TRequest request, RequestContext requestContext, Publication publication, CancellationToken cancellationToken = default)
        {
            requestContext.Span ??= Activity.Current;
            
            MessageMapper.Context = requestContext; 
            var message = await MessageMapper.MapToMessageAsync(request, publication, cancellationToken); 
            await Transforms.EachAsync(async transform =>
            {
                transform.Context = requestContext;
                message = await transform.WrapAsync(message, publication, cancellationToken);
            }); 
            return message;
        }
    }
}
