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
/// <c>Post</c>s a <see cref="SharedMarkerPostedCommand"/> then <c>Send</c>s a
/// <see cref="SharedMarkerSentCommand"/> in one action (AC-34), recording the shared
/// <see cref="IMarker"/>'s dispose count immediately after <c>Send</c> returns - before the HTTP
/// response, and therefore before ASP.NET could have disposed the request scope.
/// </summary>
[ApiController]
[Route("api/shared-dependency")]
public sealed class SharedDependencyController : ControllerBase
{
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly SharedDependencyRecorder _recorder;

    public SharedDependencyController(IAmACommandProcessor commandProcessor, SharedDependencyRecorder recorder)
    {
        _commandProcessor = commandProcessor;
        _recorder = recorder;
    }

    [HttpPost]
    public IActionResult Combine()
    {
        _commandProcessor.Post(new SharedMarkerPostedCommand());
        _commandProcessor.Send(new SharedMarkerSentCommand());
        _recorder.RecordDisposeCountAfterAction(_recorder.MapperMarker!.DisposeCount);
        return Ok();
    }
}
