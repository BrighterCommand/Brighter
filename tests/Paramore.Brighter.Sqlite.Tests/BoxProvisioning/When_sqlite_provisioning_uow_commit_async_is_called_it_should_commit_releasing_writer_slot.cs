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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

/// <summary>
/// CommitAsync commits the BEGIN IMMEDIATE transaction, which is what releases SQLite's writer slot
/// (ADR 0058 §B.1); a completed transaction throws when committed again.
/// </summary>
public class ProvisioningUnitOfWorkCommitTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_uow_commit_{Guid.NewGuid():N}.db");

    private readonly string _connectionString;
    private readonly SqliteConnection _connection;

    public ProvisioningUnitOfWorkCommitTests()
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
    public async Task When_sqlite_provisioning_uow_commit_async_is_called_it_should_commit_releasing_writer_slot()
    {
        // Arrange
        await using var uow = new SqliteProvisioningUnitOfWork(_connection, NullLogger.Instance);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);
        var transaction = uow.Transaction!;

        // Act
        await uow.CommitAsync(CancellationToken.None);

        // Assert
        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }
}
