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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A transform that records construction and disposal identity and order via an injected
/// <see cref="ConstructionOrderRecorder"/>, so a test can assert not just that two DI resolutions are
/// distinct but that the first was disposed before the second was constructed. Implements both
/// <see cref="IAmAMessageTransform"/> and <see cref="IAmAMessageTransformAsync"/> so the same double
/// serves both the sync/Reactor and async/Proactor transform pipeline builders.
/// </summary>
public sealed class TrackingTransform : IAmAMessageTransform, IAmAMessageTransformAsync
{
    private readonly ConstructionOrderRecorder _recorder;
    private readonly int _id;

    public TrackingTransform(ConstructionOrderRecorder recorder)
    {
        _recorder = recorder;
        _id = recorder.RecordConstruction();
    }

    public IRequestContext? Context { get; set; }

    public void Dispose() => _recorder.RecordDisposal(_id);

    public void InitializeWrapFromAttributeParams(params object[] initializerList) { }

    public void InitializeUnwrapFromAttributeParams(params object[] initializerList) { }

    public Message Wrap(Message message, Publication publication) => message;

    public Message Unwrap(Message message) => message;

    public Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken)
        => Task.FromResult(message);

    public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
        => Task.FromResult(message);
}
