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

public class StableLockKeyIdentityTests : IAsyncLifetime
{
    // The key derivation is a pure deterministic function of the lock key (SHA-256, big-endian),
    // so a fixed lock key must map to the same 64-bit advisory-lock identity on every acquisition
    // (AC-6, FR-4). Acquiring a fixed key, releasing it, and re-acquiring it on the same session,
    // then observing identical pg_locks (classid, objid) both times (with objsubid = 1, the
    // single-arg bigint overload), demonstrates the derivation is stable across repeated use.

    // Pooling is disabled so the session is a fresh backend with no residual advisory locks,
    // making the per-session pg_locks observation deterministic.
    private readonly string _connectionString =
        PostgreSqlSettings.TestsBrighterConnectionString + "Pooling=false;";

    private readonly string _lockKey = $"BrighterMigrationStableKeyTest_{Guid.NewGuid():N}";

    private NpgsqlConnection? _connection;

    [Fact]
    public async Task When_a_fixed_lock_key_is_acquired_released_and_reacquired_it_should_map_to_the_same_advisory_lock_identity()
    {
        // Arrange — ensure the database exists and open a single backend session.
        new PostgresSqlTestHelper().SetupDatabase();

        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();

        var advisoryLock = new PostgreSqlAdvisoryLock();
        var timeout = TimeSpan.FromSeconds(30);

        // Act — acquire the fixed key and capture its advisory-lock identity.
        await advisoryLock.AcquireAsync(_connection, _lockKey, timeout, CancellationToken.None);
        var firstIdentity = await SingleAdvisoryLockIdentity(_connection);

        // Act — release, then re-acquire the same fixed key and capture its identity again.
        Assert.True(await advisoryLock.ReleaseAsync(_connection, _lockKey, CancellationToken.None));
        await advisoryLock.AcquireAsync(_connection, _lockKey, timeout, CancellationToken.None);
        var secondIdentity = await SingleAdvisoryLockIdentity(_connection);

        // Assert — both acquisitions used the single-arg bigint overload and produced the identical
        //          (classid, objid) identity, proving the derivation is stable for a fixed key.
        Assert.Equal(1, firstIdentity.Objsubid);
        Assert.Equal(1, secondIdentity.Objsubid);
        Assert.Equal((firstIdentity.Classid, firstIdentity.Objid),
            (secondIdentity.Classid, secondIdentity.Objid));

        // Cleanup — release the re-acquired lock.
        Assert.True(await advisoryLock.ReleaseAsync(_connection, _lockKey, CancellationToken.None));
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
            if (_connection is not null) await _connection.DisposeAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
