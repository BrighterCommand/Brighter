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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Starts a real <see cref="IDispatcher"/> from inside its own action - a live HTTP request, not host
/// startup - and blocks until a known batch of messages has been consumed, so a test can prove a consumer
/// pipeline started this way still never adopts the request's own ambient scope (AC-55). Records its own
/// <see cref="ControllerBase.HttpContext"/> into an injected <see cref="DispatcherFromRequestRecorder"/>
/// before starting the pump, so a test can compare it against what each pipeline observed.
/// </summary>
[ApiController]
[Route("api/dispatcher-from-request")]
public sealed class StartDispatcherFromRequestController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly CountdownEvent _countdown;
    private readonly DispatcherFromRequestRecorder _recorder;

    public StartDispatcherFromRequestController(
        IDispatcher dispatcher,
        CountdownEvent countdown,
        DispatcherFromRequestRecorder recorder)
    {
        _dispatcher = dispatcher;
        _countdown = countdown;
        _recorder = recorder;
    }

    [HttpPost]
    public async Task<IActionResult> Start()
    {
        _recorder.RecordController(HttpContext);

        _dispatcher.Receive();
        var allProcessed = _countdown.Wait(TimeSpan.FromSeconds(30));
        await _dispatcher.End();

        return allProcessed ? Ok() : StatusCode(StatusCodes.Status500InternalServerError);
    }
}
