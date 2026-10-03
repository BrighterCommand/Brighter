#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    /// <summary>
    /// An <see cref="IAmAHandlerFactorySync"/> double that records every <see cref="Release"/> onto a
    /// shared <see cref="ReleaseOrderRecorder"/>, throws for one designated <see cref="RecordingHandler"/>,
    /// and offers a pre-built <see cref="IAmAScope"/> handle from <see cref="CreatePipelineScope"/>.
    /// </summary>
    public sealed class RecordingReleaseHandlerFactory(ReleaseOrderRecorder recorder, IAmAScope? pipelineScope)
        : IAmAHandlerFactorySync
    {
        private RecordingHandler? _throwFor;

        /// <summary>Designates the handler whose <see cref="Release"/> call should throw.</summary>
        public void ThrowFor(RecordingHandler handler) => _throwFor = handler;

        public IAmAScope? CreatePipelineScope() => pipelineScope;

        public IHandleRequests? Create(Type handlerType, IAmALifetime lifetime) =>
            throw new NotSupportedException("This double's handlers are added to the lifetime directly by the test.");

        public void Release(IHandleRequests handler, IAmALifetime lifetime)
        {
            var recordingHandler = (RecordingHandler)handler;
            recorder.RecordRelease(recordingHandler.Label);

            if (ReferenceEquals(handler, _throwFor))
                throw new InvalidOperationException($"Release failed for handler '{recordingHandler.Label}'.");
        }
    }
}
