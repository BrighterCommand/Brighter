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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter
{
    /// <summary>
    /// class UnwrapPipeline
    /// A pipeline with a sink of a <see cref="MessageMapper"/> that:
    /// Takes a message and maps it to a request
    /// Runs transforms on that message
    /// </summary>
    public class UnwrapPipeline<TRequest> : TransformPipeline<TRequest> where TRequest: class, IRequest
    {
        /// <summary>
        /// Constructs an instance of an Unwrap pipeline
        /// </summary>
        /// <param name="transforms">The transforms that run before the mapper</param>
        /// <param name="messageTransformerFactory">The factory used to create transforms</param>
        /// <param name="messageMapper">The message mapper that forms the pipeline sink</param>
        public UnwrapPipeline(
            IEnumerable<IAmAMessageTransformAsync> transforms, 
            IAmAMessageTransformerFactory messageTransformerFactory, 
            IAmAMessageMapper<TRequest> messageMapper)
        {
            MessageMapper = messageMapper;
            Transforms = transforms;
            if (messageTransformerFactory != null)
            {
                InstanceScope = new TransformLifetimeScope(messageTransformerFactory);
                Transforms.Each(transform => InstanceScope.Add(transform));
            }
        }

        /// <summary>        
        /// Lists the unwrap pipeline: filter transforms and message mapper that will be executed
        /// Used for pipeline verification
        /// </summary>
        /// <param name="pipelineTracer"></param>
        public void DescribePath(TransformPipelineTracer pipelineTracer)
        {
            Transforms.Each(t => pipelineTracer.AddTransform(t.GetType().Name));
            pipelineTracer.AddTransform(MessageMapper.GetType().Name);
        }

        /// <summary>
        /// Transforms a <see cref="Message"/> into a <see cref="IRequest"/> 
        /// Applies any required <see cref="IAmAMessageTransformAsync"/> to that <see cref="Message"/> 
        /// </summary>
        /// <param name="message">The message to unwrap</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>a request</returns>
        public async Task<TRequest> UnwrapAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
        {
            var msg = message;
            await Transforms.EachAsync(async transform => msg = await transform.UnwrapAsync(msg,cancellationToken));
            return MessageMapper.MapToRequest(msg);
        }
    }
}
