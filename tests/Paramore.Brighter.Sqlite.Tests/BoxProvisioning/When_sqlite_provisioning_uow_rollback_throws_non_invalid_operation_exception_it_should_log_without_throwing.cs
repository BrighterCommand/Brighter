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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Paramore.Brighter.Sqlite.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

/// <summary>
/// Companion to <c>ProvisioningUnitOfWorkRollbackTests</c>:
/// that test pins the post-finalised-commit path (<see cref="InvalidOperationException"/>);
/// this test pins the broader contract — RollbackAsync MUST NOT throw FOR ANY exception type.
/// Sibling backends (MSSQL
/// <c>When_mssql_provisioning_uow_rollback_throws_non_invalid_operation_exception_it_should_log_without_throwing</c>,
/// Postgres <c>When_postgres_provisioning_uow_rollback_release_throws_it_should_log_without_throwing</c>)
/// already catch <see cref="Exception"/> on the equivalent unwind seam; SQLite caught only
/// <see cref="InvalidOperationException"/>, which let a zombied-connection
/// <see cref="ObjectDisposedException"/> — or cancellation — escape and mask the runner's
/// primary migration error.
/// </summary>
/// <remarks>
/// We force a non-<see cref="InvalidOperationException"/> by handing <c>uow.RollbackAsync</c>
/// a pre-cancelled token: Microsoft.Data.Sqlite's <c>SqliteTransaction.RollbackAsync</c>
/// short-circuits on a cancelled token via <c>Task.FromCanceled</c>, surfacing
/// <see cref="TaskCanceledException"/> on await, which the old narrow catch did not handle.
/// This is the simplest deterministic surface; the same widened catch covers the
/// zombied-connection cases the reviewer cited.
/// </remarks>
public class ProvisioningUnitOfWorkRollbackNonInvalidOperationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_uow_rollback_non_ioe_{Guid.NewGuid():N}.db");

    private readonly string _connectionString;
    private readonly SqliteConnection _connection;

    public ProvisioningUnitOfWorkRollbackNonInvalidOperationTests()
    {
        _connectionString = $"Data Source={_dbPath}";
        _connection = new SqliteConnection(_connectionString);
    }

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        SqliteConnection.ClearAllPools();
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) { File.Delete(path); } } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task When_sqlite_provisioning_uow_rollback_throws_non_invalid_operation_exception_it_should_log_without_throwing()
    {
        var capturingLogger = new CapturingLogger();
        await using var uow = new SqliteProvisioningUnitOfWork(_connection, capturingLogger);
        await uow.BeginAsync(
            lockResource: "test_lock_resource_non_ioe_rollback",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Pre-cancel the token handed to RollbackAsync. SqliteTransaction.RollbackAsync honours
        // the token via Task.FromCanceled — a representative non-IOE surface covering the same
        // contract gap as a zombied connection raising ObjectDisposedException.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act — RollbackAsync MUST NOT throw even though the inner _transaction.RollbackAsync
        // raises TaskCanceledException. Disposal-style contract: runner's catch path
        // (catch { uow.RollbackAsync(...); throw; }) cannot have its primary exception masked.
        var thrown = await Record.ExceptionAsync(() => uow.RollbackAsync(cts.Token));

        Assert.Null(thrown);

        // Best-effort path emitted a Warning carrying the original exception, matching the
        // existing finalised-tx branch's diagnostic shape.
        var warnings = capturingLogger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
    }
}
