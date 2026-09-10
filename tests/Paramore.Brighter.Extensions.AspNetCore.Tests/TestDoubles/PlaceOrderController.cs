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

using Microsoft.AspNetCore.Mvc;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Captures its own <see cref="IOrderDbContext"/> and <c>Send</c>s a <see cref="PlaceOrder"/> command
/// (AC-15), recording its own instance into an injected <see cref="OrderDbContextRecorder"/> so a test
/// can compare it against what the handler resolved.
/// </summary>
[ApiController]
[Route("api/orders")]
public sealed class PlaceOrderController : ControllerBase
{
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly OrderDbContextRecorder _recorder;

    public PlaceOrderController(
        IAmACommandProcessor commandProcessor,
        IOrderDbContext orderDbContext,
        OrderDbContextRecorder recorder)
    {
        _commandProcessor = commandProcessor;
        _recorder = recorder;
        _recorder.RecordController(orderDbContext);
    }

    [HttpPost]
    public IActionResult Place()
    {
        _commandProcessor.Send(new PlaceOrder());
        return Ok();
    }
}
