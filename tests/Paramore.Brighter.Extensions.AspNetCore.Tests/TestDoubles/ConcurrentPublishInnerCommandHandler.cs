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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// An async handler for <see cref="ConcurrentPublishInnerCommand"/> that records the
/// <see cref="IOrderDbContext"/> it was constructed with into an injected
/// <see cref="ConcurrentPublishRecorder"/> (AC-12), so a test can assert a nested <c>SendAsync</c>
/// issued from inside a Publish subscriber resolves neither the request scope's instance nor either
/// subscriber's own.
/// </summary>
public sealed class ConcurrentPublishInnerCommandHandler : RequestHandlerAsync<ConcurrentPublishInnerCommand>
{
    public ConcurrentPublishInnerCommandHandler(IOrderDbContext orderDbContext, ConcurrentPublishRecorder recorder)
    {
        recorder.RecordInnerCommandInstance(orderDbContext);
    }

    public override async Task<ConcurrentPublishInnerCommand> HandleAsync(
        ConcurrentPublishInnerCommand command, CancellationToken cancellationToken = default)
        => await base.HandleAsync(command, cancellationToken);
}
