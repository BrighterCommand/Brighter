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
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A spy implementation of <see cref="IAmAPipelineValidator"/> that records calls to a shared
/// action log for verifying call ordering, and returns a configurable result.
/// </summary>
public class SpyPipelineValidator : IAmAPipelineValidator
{
    private readonly PipelineValidationResult _result;
    private readonly List<string> _actionLog;

    public bool ValidateWasCalled { get; private set; }

    public SpyPipelineValidator(PipelineValidationResult result, List<string> actionLog)
    {
        _result = result;
        _actionLog = actionLog;
    }

    public static SpyPipelineValidator WithNoErrors(List<string> actionLog) =>
        new(new PipelineValidationResult([], []), actionLog);

    public static SpyPipelineValidator WithWarningsOnly(List<string> actionLog, params ValidationError[] warnings) =>
        new(new PipelineValidationResult([], warnings), actionLog);

    public static SpyPipelineValidator WithErrors(List<string> actionLog, params ValidationError[] errors) =>
        new(new PipelineValidationResult(errors, []), actionLog);

    public PipelineValidationResult Validate()
    {
        ValidateWasCalled = true;
        _actionLog.Add("Validate");
        return _result;
    }
}
