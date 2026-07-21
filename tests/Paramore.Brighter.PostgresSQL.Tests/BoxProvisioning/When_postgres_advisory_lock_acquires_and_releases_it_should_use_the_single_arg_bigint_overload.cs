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
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlAdvisoryLockBigintOverloadTests
{
    // PostgreSqlAdvisoryLock derives a SHA-256 64-bit key in C# and locks via the single-argument
    // pg_try_advisory_lock(bigint) / pg_advisory_unlock(bigint) overloads (ADR 0062). PostgreSQL
    // records the overload arity in pg_locks.objsubid: the single-argument bigint overload records
    // objsubid = 1, whereas the legacy two-int4 overload (with hashtext) records objsubid = 2.
    // Observing objsubid = 1 from the same backend session while the lock is held therefore proves
    // the bigint overload was used and hashtext / the (int4, int4) form was not (AC-1, AC-2, AC-12).
    // Pooling is disabled so the test runs on a fresh backend with no residual advisory locks from
    // a recycled pooled session, making the pg_locks observation deterministic.
    private readonly string _connectionString =
        PostgreSqlSettings.TestsBrighterConnectionString + "Pooling=false;";

    private readonly string _lockKey = $"BrighterMigrationBigintOverloadTest_{Guid.NewGuid():N}";

    private NpgsqlConnection? _connection;

    [Test]
    public async Task When_postgres_advisory_lock_acquires_and_releases_it_should_use_the_single_arg_bigint_overload()
    {
        // Arrange — ensure the database exists and open a single backend session on which the
        //           system under test will acquire, and on which we will observe, the lock.
        new PostgresSqlTestHelper().SetupDatabase();

        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();

        var advisoryLock = new PostgreSqlAdvisoryLock();

        // Act — acquire the advisory lock on this session.
        await advisoryLock.AcquireAsync(
            _connection, _lockKey, TimeSpan.FromSeconds(30), CancellationToken.None);

        // Assert — exactly one advisory lock is held by this backend, and its objsubid is 1,
        //          proving the single-argument bigint overload (not the two-int4 / hashtext form).
        var heldObjsubid = await SingleAdvisoryLockObjsubidOrNull(_connection);
        await Assert.That(heldObjsubid).IsEqualTo(1);

        // Act — release the lock using the same key.
        var released = await advisoryLock.ReleaseAsync(_connection, _lockKey, CancellationToken.None);

        // Assert — release reports success and the advisory lock is gone from this session,
        //          confirming acquire and release named the same derived bigint key.
        await Assert.That(released).IsTrue();
        var objsubidAfterRelease = await SingleAdvisoryLockObjsubidOrNull(_connection);
        await Assert.That(objsubidAfterRelease).IsNull();
    }

    // Returns the objsubid of the single advisory lock held by this backend session, or null if
    // none is held. Asserts there is at most one advisory row so an unexpected residual lock is
    // surfaced rather than silently ignored.
    private static async Task<int?> SingleAdvisoryLockObjsubidOrNull(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT objsubid FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid()";

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var objsubid = reader.GetInt32(0);
        await Assert.That(await reader.ReadAsync()).IsFalse().Because("Expected at most one advisory lock on this session.");
        return objsubid;
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}