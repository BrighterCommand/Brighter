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
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning.TestDoubles;

/// <summary>
/// Test double for <see cref="IMsSqlAdvisoryLock"/>: <see cref="AcquireAsync"/> records the
/// lock resource it was called with and either throws the configured exception (driving the
/// runner's distinguishable-exception-propagation path per ADR 0057 §5b) or returns success
/// when no exception is configured. The MSSQL abstraction is acquire-only because
/// <c>sp_getapplock</c> is invoked with <c>@LockOwner='Transaction'</c>, so the lock is
/// auto-released on the surrounding transaction's commit or rollback.
/// </summary>
internal sealed class FakeMsSqlAdvisoryLock(Exception? throwOnAcquire) : IMsSqlAdvisoryLock
{
    public string? AcquiredResource { get; private set; }
    public SqlTransaction? CapturedTransaction { get; private set; }

    public Task AcquireAsync(
        SqlConnection connection, SqlTransaction transaction, string lockResource,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        AcquiredResource = lockResource;
        CapturedTransaction = transaction;
        if (throwOnAcquire is not null) throw throwOnAcquire;
        return Task.CompletedTask;
    }
}
