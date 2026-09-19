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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records, in resolution order, every <see cref="MixedHostConsumerMapper"/>,
/// <see cref="MixedHostConsumerTransform"/> and <see cref="MixedHostConsumerCommandHandler"/>
/// constructed while a mixed host consumes a batch of messages. Register as a singleton in the
/// container under test, so a test can assert how many distinct instances were resolved, and that each
/// was disposed, once the batch has finished processing.
/// </summary>
public sealed class MixedHostConsumerRecorder
{
    private readonly List<MixedHostConsumerMapper> _mappers = new();
    private readonly List<MixedHostConsumerTransform> _transforms = new();
    private readonly List<MixedHostConsumerCommandHandler> _handlers = new();

    /// <summary>
    /// Every <see cref="MixedHostConsumerMapper"/> constructed, in order.
    /// </summary>
    public IReadOnlyList<MixedHostConsumerMapper> Mappers
    {
        get { lock (_mappers) return _mappers.ToArray(); }
    }

    /// <summary>
    /// Every <see cref="MixedHostConsumerTransform"/> constructed, in order.
    /// </summary>
    public IReadOnlyList<MixedHostConsumerTransform> Transforms
    {
        get { lock (_transforms) return _transforms.ToArray(); }
    }

    /// <summary>
    /// Every <see cref="MixedHostConsumerCommandHandler"/> constructed, in order.
    /// </summary>
    public IReadOnlyList<MixedHostConsumerCommandHandler> Handlers
    {
        get { lock (_handlers) return _handlers.ToArray(); }
    }

    /// <summary>
    /// Records a <see cref="MixedHostConsumerMapper"/> construction.
    /// </summary>
    public void RecordMapper(MixedHostConsumerMapper mapper)
    {
        lock (_mappers) _mappers.Add(mapper);
    }

    /// <summary>
    /// Records a <see cref="MixedHostConsumerTransform"/> construction.
    /// </summary>
    public void RecordTransform(MixedHostConsumerTransform transform)
    {
        lock (_transforms) _transforms.Add(transform);
    }

    /// <summary>
    /// Records a <see cref="MixedHostConsumerCommandHandler"/> construction.
    /// </summary>
    public void RecordHandler(MixedHostConsumerCommandHandler handler)
    {
        lock (_handlers) _handlers.Add(handler);
    }
}
