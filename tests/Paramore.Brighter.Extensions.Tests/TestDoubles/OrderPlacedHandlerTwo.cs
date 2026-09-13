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
/// The second of three subscribers to <see cref="OrderPlaced"/>. Records the <see cref="IUnitOfWork"/> it
/// was constructed with into an injected <see cref="UnitOfWorkRecorder"/>, so a test can assert every
/// subscriber resolved its own distinct <see cref="IUnitOfWork"/> (AC-10).
/// </summary>
public sealed class OrderPlacedHandlerTwo : RequestHandlerAsync<OrderPlaced>
{
    private readonly UnitOfWorkRecorder _recorder;

    public OrderPlacedHandlerTwo(IUnitOfWork unitOfWork, UnitOfWorkRecorder recorder)
    {
        _recorder = recorder;
        _recorder.Record(unitOfWork);
    }

    public override async Task<OrderPlaced> HandleAsync(OrderPlaced @event, CancellationToken cancellationToken = default)
        => await base.HandleAsync(@event, cancellationToken);
}
