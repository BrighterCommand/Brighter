#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
namespace Paramore.Brighter.Observability;

/// <summary>
/// How one stale-message count ended at one due time.
/// </summary>
/// <remarks>
/// Public for Brighter's own use by the outbox stale-message monitor and its meter, not as an extension point.
/// </remarks>
public enum StaleMeasurementOutcome
{
    /// <summary>The count finished and returned a value.</summary>
    Completed,

    /// <summary>The count threw an exception.</summary>
    Failed,

    /// <summary>The count was still running at its deadline, so it was cancelled and any later result ignored.</summary>
    Abandoned,

    /// <summary>The due time passed while the count's previous call was still in progress, so no call started.</summary>
    Skipped
}
