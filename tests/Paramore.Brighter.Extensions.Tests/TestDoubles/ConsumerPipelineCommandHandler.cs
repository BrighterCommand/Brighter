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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A sync handler for <see cref="ConsumerPipelineCommand"/>, consumed by a real message pump. Records
/// the <see cref="IUnitOfWork"/> it was constructed with into an injected <see cref="UnitOfWorkRecorder"/>,
/// so a test can inspect what a consumer pipeline resolved after the pump has processed it, and signals
/// an injected <see cref="ConsumerPipelineSignal"/> so the test's own thread can tell when processing
/// completes without polling.
/// </summary>
public sealed class ConsumerPipelineCommandHandler : RequestHandler<ConsumerPipelineCommand>
{
    private readonly ConsumerPipelineSignal _signal;

    public ConsumerPipelineCommandHandler(IUnitOfWork unitOfWork, UnitOfWorkRecorder recorder, ConsumerPipelineSignal signal)
    {
        recorder.Record(unitOfWork);
        _signal = signal;
    }

    public override ConsumerPipelineCommand Handle(ConsumerPipelineCommand command)
    {
        var result = base.Handle(command);
        _signal.Signal();
        return result;
    }
}
