#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class EmptyMessageTransformerFactory.
    /// This class acts as a null object for IAmAMessageTransformerFactory. It is used when no transformer is required, and allows us to avoid null checks in the pipeline.
    /// </summary>
    public class EmptyMessageTransformerFactory : IAmAMessageTransformerFactory
    {
        public IAmAMessageTransform Create(Type transformerType) { return new EmptyMessageTransform(); }

        public void Release(IAmAMessageTransform transformer) { transformer.Dispose(); }
    }

    /// <summary>
    /// Empty Transform - a null object for IAmAMessageTransform. It is used when no transformer is required, and allows us to avoid null checks in the pipeline.
    /// </summary>
    public class EmptyMessageTransform : IAmAMessageTransform
    {
        public IRequestContext? Context { get; set; }
        public void Dispose() {GC.SuppressFinalize(this);}
        public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }
        public Message Wrap(Message message, Publication publication) { return message; }
        public Message Unwrap(Message message) { return message; }
    }
}
