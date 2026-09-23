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
using System.Collections;
using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.MessagingGateway.TestDoubles;

/// <summary>
/// An <see cref="IEnumerable{T}"/> of <see cref="IAmAChannelFactory"/> that can be enumerated
/// exactly once; a second <see cref="GetEnumerator"/> call throws. Used to prove that a caller
/// reads the sequence only once, rather than relying on a collection expression or array, which
/// would re-enumerate successfully and hide the defect.
/// </summary>
public class SinglePassChannelFactorySequence(params IAmAChannelFactory[] factories) : IEnumerable<IAmAChannelFactory>
{
    private bool _enumerated;

    public IEnumerator<IAmAChannelFactory> GetEnumerator()
    {
        if (_enumerated)
            throw new InvalidOperationException("This sequence has already been enumerated once.");

        _enumerated = true;
        return ((IEnumerable<IAmAChannelFactory>)factories).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
