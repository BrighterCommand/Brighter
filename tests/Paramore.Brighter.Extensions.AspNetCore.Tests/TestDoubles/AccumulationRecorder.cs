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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records the most recently constructed <see cref="AccumulationPostedMapper"/>, overwriting on every
/// construction rather than accumulating a list (AC-37) - this recorder must not itself be the reason a
/// weak reference to an earlier request's mapper stays alive. Register as a singleton in the container
/// under test.
/// </summary>
public sealed class AccumulationRecorder
{
    /// <summary>
    /// The most recently constructed <see cref="AccumulationPostedMapper"/>.
    /// </summary>
    public AccumulationPostedMapper? LastConstructedMapper { get; private set; }

    /// <summary>
    /// Records a newly constructed <see cref="AccumulationPostedMapper"/>, replacing whatever this
    /// recorder held before.
    /// </summary>
    public void RecordConstruction(AccumulationPostedMapper mapper) => LastConstructedMapper = mapper;
}
