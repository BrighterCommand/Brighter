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

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Captures its own <see cref="IOrderDbContext"/> into an injected <see cref="ConcurrentPublishRecorder"/>
/// and <c>PublishAsync</c>s a <see cref="ConcurrentPublishOrderPlaced"/> to its two subscribers (AC-12),
/// then <c>Send</c>s and <c>Post</c>s once the publish has returned, so a test can assert the caller's
/// own flow was not left suppressed by it.
/// </summary>
[ApiController]
[Route("api/concurrent-publish")]
public sealed class ConcurrentPublishController : ControllerBase
{
    private readonly IAmACommandProcessor _commandProcessor;

    public ConcurrentPublishController(
        IAmACommandProcessor commandProcessor, IOrderDbContext orderDbContext, ConcurrentPublishRecorder recorder)
    {
        _commandProcessor = commandProcessor;
        recorder.RecordRequestScopeInstance(orderDbContext);
    }

    [HttpPost]
    public async Task<IActionResult> Publish()
    {
        await _commandProcessor.PublishAsync(new ConcurrentPublishOrderPlaced(new ConcurrentPublishRendezvous()));

        _commandProcessor.Send(new ConcurrentPublishSendCommand());
        _commandProcessor.Post(new ConcurrentPublishPostedCommand());

        return Ok();
    }
}
