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
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class DistinctLockKeyIdentityTests : IAsyncLifetime
{
    // Distinct Brighter lock keys must derive distinct 64-bit advisory-lock keys, so two migrations
    // for different schemas acquire on different advisory-lock identities and never block one
    // another (AC-4). PostgreSQL exposes the 64-bit single-arg key in pg_locks as (classid, objid)
    // — the high and low 32 bits of the bigint. Acquiring two schema-distinct keys on two separate
    // backend sessions and observing two different (classid, objid) pairs, with neither acquire
    // timing out, demonstrates the distinctness and the absence of contention.

    // Pooling is disabled so each session is a fresh backend with no residual advisory locks,
    // making the per-session pg_locks observation deterministic.
    private readonly string _connectionString =
        PostgreSqlSettings.TestsBrighterConnectionString + "Pooling=false;";

    // A per-run suffix keeps the two keys unique to this test run (avoiding contention with any
    // concurrent test) while preserving the schema-distinct shape the runner produces.
    private readonly string _runId = Guid.NewGuid().ToString("N");

    private NpgsqlConnection? _sessionA;
    private NpgsqlConnection? _sessionB;

    [Fact]
    public async Task When_two_distinct_lock_keys_are_acquired_they_should_occupy_distinct_advisory_lock_identities()
    {
        // Arrange — ensure the database exists and open two independent backend sessions, each of
        //           which will hold one of the two schema-distinct migration locks.
        new PostgresSqlTestHelper().SetupDatabase();

        var lockKeyA = $"BrighterMigration_public.Outbox_{_runId}";
        var lockKeyB = $"BrighterMigration_billing.Outbox_{_runId}";

        _sessionA = new NpgsqlConnection(_connectionString);
        await _sessionA.OpenAsync();
        _sessionB = new NpgsqlConnection(_connectionString);
        await _sessionB.OpenAsync();

        var advisoryLock = new PostgreSqlAdvisoryLock();
        var timeout = TimeSpan.FromSeconds(5);

        // Act — acquire each key on its own session. Neither call may block on the other: distinct
        //       keys derive distinct locks, so a 5s timeout is ample and a timeout would mean the
        //       two keys collided onto a shared lock.
        await advisoryLock.AcquireAsync(_sessionA, lockKeyA, timeout, CancellationToken.None);
        await advisoryLock.AcquireAsync(_sessionB, lockKeyB, timeout, CancellationToken.None);

        var identityA = await SingleAdvisoryLockIdentity(_sessionA);
        var identityB = await SingleAdvisoryLockIdentity(_sessionB);

        // Assert — both used the single-arg bigint overload and the two derived identities differ.
        Assert.Equal(1, identityA.Objsubid);
        Assert.Equal(1, identityB.Objsubid);
        Assert.NotEqual((identityA.Classid, identityA.Objid), (identityB.Classid, identityB.Objid));

        // Act + Assert — both release cleanly using their own keys.
        Assert.True(await advisoryLock.ReleaseAsync(_sessionA, lockKeyA, CancellationToken.None));
        Assert.True(await advisoryLock.ReleaseAsync(_sessionB, lockKeyB, CancellationToken.None));
    }

    private static async Task<(long Classid, long Objid, int Objsubid)> SingleAdvisoryLockIdentity(
        NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        // classid/objid are oid (unsigned 32-bit); cast to bigint so Npgsql can read them as long.
        command.CommandText =
            "SELECT classid::bigint, objid::bigint, objsubid FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid()";

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected one advisory lock held on this session.");
        var identity = (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2));
        Assert.False(await reader.ReadAsync(), "Expected exactly one advisory lock on this session.");
        return identity;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            if (_sessionA is not null) await _sessionA.DisposeAsync();
            if (_sessionB is not null) await _sessionB.DisposeAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
