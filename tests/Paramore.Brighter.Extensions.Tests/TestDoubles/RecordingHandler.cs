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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A bare <see cref="IHandleRequests"/> double, distinguished only by <see cref="Label"/>, for tests that
/// track handler instances directly on a <see cref="HandlerLifetimeScope"/> rather than through a built
/// pipeline.
/// </summary>
public sealed class RecordingHandler(string label) : IHandleRequests
{
    /// <summary>The label this instance was constructed with, for identifying it in test assertions.</summary>
    public string Label { get; } = label;

    public IRequestContext? Context { get; set; }

    public HandlerName Name => new(Label);

    public void DescribePath(IAmAPipelineTracer pathExplorer) { }

    public void InitializeFromAttributeParams(params object?[] initializerList) { }

    public void AddToLifetime(IAmALifetime instanceScope) { }
}
