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
/// <c>Post</c>s two <see cref="AccumulationPostedCommand"/>s and <c>Send</c>s one
/// <see cref="AccumulationSentCommand"/> in one action (AC-37), so a sustained-load test can drive many
/// requests through the same borrowed-scope shape T6.5/T6.6 already established.
/// </summary>
[ApiController]
[Route("api/accumulate")]
public sealed class AccumulationController : ControllerBase
{
    private readonly IAmACommandProcessor _commandProcessor;

    public AccumulationController(IAmACommandProcessor commandProcessor) => _commandProcessor = commandProcessor;

    [HttpPost]
    public IActionResult Accumulate()
    {
        _commandProcessor.Post(new AccumulationPostedCommand());
        _commandProcessor.Post(new AccumulationPostedCommand());
        _commandProcessor.Send(new AccumulationSentCommand());
        return Ok();
    }
}
