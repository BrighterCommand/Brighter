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

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Thrown when SQL Server selects the migration-lock acquisition as a deadlock victim
/// (<c>sp_getapplock</c> return code <c>-3</c>). Distinct from <see cref="TimeoutException"/>
/// (return code <c>-1</c>) so an operator can choose a different recovery strategy — typically
/// exponential backoff with retry rather than immediate deployment failure, since deadlock
/// victims indicate transient contention rather than persistent unavailability.
/// </summary>
public class MigrationLockDeadlockException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="MigrationLockDeadlockException"/> class
    /// with a specified message describing the lock resource and the deadlock outcome.
    /// </summary>
    /// <param name="message">A description of the deadlock outcome.</param>
    public MigrationLockDeadlockException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="MigrationLockDeadlockException"/> class
    /// with a specified message and inner exception.
    /// </summary>
    /// <param name="message">A description of the deadlock outcome.</param>
    /// <param name="innerException">The exception that triggered this deadlock report.</param>
    public MigrationLockDeadlockException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
