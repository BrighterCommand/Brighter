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
using Microsoft.AspNetCore.Mvc;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// <c>Post</c>s a <see cref="FailedBuildPostedCommand"/> 100 times in one action, each expected to throw
/// (AC-38), recording each outcome into an injected <see cref="FailedBuildRecorder"/>. Uses its own
/// <see cref="IOrderDbContext"/> again once every attempt has failed, before returning, so a test can
/// prove the request scope was not disposed by any of the failed builds.
/// </summary>
[ApiController]
[Route("api/failed-builds")]
public sealed class FailedBuildController : ControllerBase
{
    private const int Attempts = 100;

    private readonly IAmACommandProcessor _commandProcessor;
    private readonly IOrderDbContext _orderDbContext;
    private readonly FailedBuildRecorder _recorder;

    public FailedBuildController(
        IAmACommandProcessor commandProcessor,
        IOrderDbContext orderDbContext,
        FailedBuildRecorder recorder)
    {
        _commandProcessor = commandProcessor;
        _orderDbContext = orderDbContext;
        _recorder = recorder;
    }

    [HttpPost]
    public IActionResult PostRepeatedlyFailingCommands()
    {
        for (var i = 0; i < Attempts; i++)
        {
            try
            {
                _commandProcessor.Post(new FailedBuildPostedCommand());
                _recorder.RecordUnexpectedOutcome();
            }
            catch (ConfigurationException ex) when (ex.InnerException is InvalidOperationException)
            {
                _recorder.RecordExpectedFailure();
            }
            catch (Exception)
            {
                _recorder.RecordUnexpectedOutcome();
            }
        }

        try
        {
            _orderDbContext.EnsureUsable();
            _recorder.RecordRequestScopeStateAfterFailures(usable: true, _orderDbContext.DisposeCount);
        }
        catch (ObjectDisposedException)
        {
            _recorder.RecordRequestScopeStateAfterFailures(usable: false, _orderDbContext.DisposeCount);
        }

        return Ok();
    }
}
