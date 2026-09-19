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

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records, in resolution order, every <see cref="DispatcherFromRequestMapper"/> and
/// <see cref="DispatcherFromRequestHandler"/> constructed while a <c>Dispatcher</c> started from inside a
/// live request consumes a batch of messages - and the <see cref="HttpContext"/> each handler's own
/// <c>IHttpContextAccessor</c> observed at construction, so a test can prove the pump's flow genuinely
/// carried a live, non-null ambient even though it never adopted it (AC-55). Register as a singleton in
/// the container under test.
/// </summary>
public sealed class DispatcherFromRequestRecorder
{
    private readonly List<DispatcherFromRequestMapper> _mappers = new();
    private readonly List<DispatcherFromRequestHandler> _handlers = new();
    private readonly List<HttpContext?> _observedHttpContexts = new();

    /// <summary>
    /// The <see cref="HttpContext"/> the controller that started the pump was itself serving, so a test
    /// can compare it against each pipeline's own observed context.
    /// </summary>
    public HttpContext? ControllerHttpContext { get; private set; }

    /// <summary>
    /// Every <see cref="DispatcherFromRequestMapper"/> constructed, in order.
    /// </summary>
    public IReadOnlyList<DispatcherFromRequestMapper> Mappers
    {
        get { lock (_mappers) return _mappers.ToArray(); }
    }

    /// <summary>
    /// Every <see cref="DispatcherFromRequestHandler"/> constructed, in order.
    /// </summary>
    public IReadOnlyList<DispatcherFromRequestHandler> Handlers
    {
        get { lock (_handlers) return _handlers.ToArray(); }
    }

    /// <summary>
    /// The <see cref="HttpContext"/> each <see cref="DispatcherFromRequestHandler"/> observed via its own
    /// injected <c>IHttpContextAccessor</c> at construction, in the same order as <see cref="Handlers"/>.
    /// </summary>
    public IReadOnlyList<HttpContext?> ObservedHttpContexts
    {
        get { lock (_observedHttpContexts) return _observedHttpContexts.ToArray(); }
    }

    /// <summary>
    /// Records the controller's own <see cref="HttpContext"/>.
    /// </summary>
    public void RecordController(HttpContext httpContext) => ControllerHttpContext = httpContext;

    /// <summary>
    /// Records a <see cref="DispatcherFromRequestMapper"/> construction.
    /// </summary>
    public void RecordMapper(DispatcherFromRequestMapper mapper)
    {
        lock (_mappers) _mappers.Add(mapper);
    }

    /// <summary>
    /// Records a <see cref="DispatcherFromRequestHandler"/> construction, and the <see cref="HttpContext"/>
    /// its own <c>IHttpContextAccessor</c> observed at that moment.
    /// </summary>
    public void RecordHandler(DispatcherFromRequestHandler handler, HttpContext? observedHttpContext)
    {
        lock (_handlers) _handlers.Add(handler);
        lock (_observedHttpContexts) _observedHttpContexts.Add(observedHttpContext);
    }
}
