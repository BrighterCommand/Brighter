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

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning.TestDoubles;

// Records its arguments before throwing, so a test can read them even when the acquire or release fails.
internal sealed class FakeMySqlAdvisoryLock(
    bool? releaseResult,
    Exception? throwOnAcquire = null,
    Exception? throwOnRelease = null) : IMySqlAdvisoryLock
{
    public string? AcquiredKey { get; private set; }
    public string? ReleasedKey { get; private set; }
    public TimeSpan? AcquiredTimeout { get; private set; }

    public Task AcquireAsync(
        MySqlConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        AcquiredKey = lockKey;
        AcquiredTimeout = timeout;
        if (throwOnAcquire is not null) throw throwOnAcquire;
        return Task.CompletedTask;
    }

    public Task<bool?> ReleaseAsync(
        MySqlConnection connection, string lockKey,
        CancellationToken cancellationToken)
    {
        ReleasedKey = lockKey;
        if (throwOnRelease is not null) throw throwOnRelease;
        return Task.FromResult(releaseResult);
    }
}
