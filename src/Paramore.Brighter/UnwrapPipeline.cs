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
    public class UnwrapPipeline<TRequest> where TRequest: class, IRequest, new()
    {
        private readonly IAmAMessageMapper _messageMapper;
        private readonly IEnumerable<IAmAMessageTransformAsync> _transforms;

        /// <summary>
        /// Constructs an instance of an Unwrap pipeline
        /// </summary>
        /// <param name="transforms">The transforms that run before the mapper</param>
        /// <param name="messageMapper">The message mapper that forms the pipeline sink</param>
        public UnwrapPipeline(IEnumerable<IAmAMessageTransformAsync> transforms, IAmAMessageMapper messageMapper)
        {
            _messageMapper = messageMapper;
            _transforms = transforms;
        }

        /// <summary>        
        /// Lists the unwrap pipeline: filter transforms and message mapper that will be executed
        /// Used for pipeline verification
        /// </summary>
        /// <param name="pipelineTracer"></param>
        public void DescribePath(TransformPipelineTracer pipelineTracer)
        {
            _transforms.Each(t => pipelineTracer.AddTransform(t.GetType().Name));
            pipelineTracer.AddTransform(_messageMapper.GetType().Name);
        }

        /// <summary>
        /// Transforms a <see cref="Message"/> into a <see cref="IRequest"/> 
        /// Applies any required <see cref="IAmAMessageTransformAsync"/> to that <see cref="Message"/> 
        /// </summary>
        /// <param name="message"></param>
        /// <returns>a request</returns>
        public async Task<TRequest> Unwrap(Message message)
        {
            return null;
        }
    }
}
