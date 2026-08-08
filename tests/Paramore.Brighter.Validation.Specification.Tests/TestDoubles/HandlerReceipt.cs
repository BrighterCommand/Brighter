#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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

namespace Paramore.Brighter.Validation.Specification.Tests.TestDoubles;

/// <summary>
/// Records that the business handler ran, so a test can assert the request reached it (valid path) or did
/// not (invalid path). Registered as a singleton so each pipeline has its own receipt and no static state is
/// shared between concurrently running tests.
/// </summary>
public class HandlerReceipt
{
    private int _handledCount;

    public bool Handled => _handledCount > 0;

    public int HandledCount => _handledCount;

    public string? HandledName { get; private set; }

    public void Record(string name)
    {
        System.Threading.Interlocked.Increment(ref _handledCount);
        HandledName = name;
    }
}
