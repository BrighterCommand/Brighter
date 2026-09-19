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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// Records, in resolution order, the <see cref="IUnitOfWork"/> each <see cref="OrderPlaced"/> subscriber
/// was constructed with. Register as a singleton in the container under test and inject into every
/// subscriber, so a test can assert what each subscriber resolved after the publish's subscriber
/// pipelines (and their owned scopes) have already been torn down.
/// </summary>
public sealed class UnitOfWorkRecorder
{
    private readonly List<IUnitOfWork> _unitsOfWork = new();

    /// <summary>
    /// The <see cref="IUnitOfWork"/> each subscriber construction recorded, in order.
    /// </summary>
    public IReadOnlyList<IUnitOfWork> UnitsOfWork
    {
        get { lock (_unitsOfWork) return _unitsOfWork.ToArray(); }
    }

    /// <summary>
    /// Records the <see cref="IUnitOfWork"/> a subscriber was constructed with.
    /// </summary>
    public void Record(IUnitOfWork unitOfWork)
    {
        lock (_unitsOfWork) _unitsOfWork.Add(unitOfWork);
    }
}
