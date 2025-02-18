using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MySql;

namespace Paramore.Brighter.Locking.MySql;

/// <summary>
/// The MySQL Locking Provider
/// </summary>
/// <param name="connectionProvider">The MySQL connection Provider.</param>
public class MySqlLockingProvider(MySqlConnectionProvider connectionProvider) : IDistributedLock, IAsyncDisposable
{
    private readonly ILogger _logger = ApplicationLogging.CreateLogger<MySqlConnectionProvider>();
    private readonly ConcurrentDictionary<string, DbConnection> _connections = new();

    /// <summary>
    /// Attempt to obtain a lock on a resource
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns>The id of the lock that has been acquired or null if no lock was able to be acquired</returns>
    public async Task<string> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        if (_connections.ContainsKey(resource))
        {
            return null;
        }

        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
#if NETSTANDARD2_0
        using var command = connection.CreateCommand();
#else
        await using var command = connection.CreateCommand();
#endif
        command.CommandText = MySqlLockingQueries.ObtainLockQuery;
        command.Parameters.Add(new MySqlParameter("@RESOURCE_NAME", MySqlDbType.String)
        {
            Value = GetSafeName(resource)
        });

        command.Parameters.Add(new MySqlParameter("@TIMEOUT", MySqlDbType.UInt32)
        {
            Value = 1 
        });

        var result = await command.ExecuteScalarAsync(cancellationToken) ?? -1;
        if (Convert.ToInt64(result) != 1)
        {
            return null;
        }

        _connections.TryAdd(resource, connection);
        return resource;
    }


    /// <summary>
    /// Release a lock
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="lockId">The lock Id that was provided when the lock was obtained</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Awaitable Task</returns>
    public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
    {
        if (!_connections.TryRemove(resource, out var connection))
        {
            return;
        }

#if NETSTANDARD2_0
        using var command = connection.CreateCommand();
#else
        await using var command = connection.CreateCommand();
#endif
        command.CommandText = MySqlLockingQueries.ReleaseLockQuery;
        command.Parameters.Add(
            new MySqlParameter("@RESOURCE_NAME", MySqlDbType.String) { Value = GetSafeName(resource) });

        await command.ExecuteNonQueryAsync(cancellationToken);

        
#if NETSTANDARD2_0
        connection.Close();
        connection.Dispose();
#else
        await connection.CloseAsync();
        await connection.DisposeAsync();
#endif
    }

    /// <summary>
    /// Dispose Locking Provider
    /// </summary>
#if NETSTANDARD2_0
    public ValueTask DisposeAsync()
    {
        foreach (var  connection in _connections.Select(x => x.Value))
        {
             connection.Dispose();
        }
        
        _connections.Clear();
        return new ValueTask();
    }
#else
    public async ValueTask DisposeAsync()
    {
        foreach ((_, DbConnection connection) in _connections)
        {
            await connection.DisposeAsync();
        }

        _connections.Clear();
    }
#endif

    // Copied from https://github.com/madelson/DistributedLock/blob/2.5.0/src/DistributedLock.MySql/MySqlDistributedLock.cs#L82-L147
    // That repo is using MIT license.
    /// <summary>
    /// From https://dev.mysql.com/doc/refman/8.0/en/locking-functions.html
    /// </summary>
    private const int MaxNameLength = 64;

    private static string GetSafeName(string name) =>
        ToSafeName(
            name,
            MaxNameLength,
            convertToValidName: s =>
            {
                if (s.Length == 0) { return "__empty__"; }

                return s.ToLowerInvariant();
            },
            hash: ComputeHash
        );

    private static string ToSafeName(string name, int maxNameLength, Func<string, string> convertToValidName,
        Func<byte[], string> hash)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        var validBaseLockName = convertToValidName(name);
        if (validBaseLockName == name && validBaseLockName.Length <= maxNameLength)
        {
            return name;
        }

        var nameHash = hash(Encoding.UTF8.GetBytes(name));

        if (nameHash.Length >= maxNameLength)
        {
            return nameHash.Substring(0, length: maxNameLength);
        }

        var prefix =
            validBaseLockName.Substring(0, Math.Min(validBaseLockName.Length, maxNameLength - nameHash.Length));
        return prefix + nameHash;
    }

    private static string ComputeHash(byte[] bytes)
    {
        using var sha = SHA512.Create();
        var hashBytes = sha.ComputeHash(bytes);

        // We truncate to 160 bits, which is 32 chars of Base32. This should still give us good collision resistance but allows for a 64-char
        // name to include a good portion of the original provided name, which is good for debugging. See
        // https://crypto.stackexchange.com/questions/9435/is-truncating-a-sha512-hash-to-the-first-160-bits-as-secure-as-using-sha1#:~:text=Yes.,time%20is%20still%20pretty%20big
        const int Base32CharBits = 5;
        const int HashLengthInChars = 160 / Base32CharBits;

        // we use Base32 because it is case-insensitive (like MySQL) and a bit more compact than Base16
        // RFC 4648 from https://en.wikipedia.org/wiki/Base32
        const string Base32Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

        var chars = new char[HashLengthInChars];
        var byteIndex = 0;
        var bitBuffer = 0;
        var bitsRemaining = 0;
        for (var charIndex = 0; charIndex < chars.Length; ++charIndex)
        {
            if (bitsRemaining < Base32CharBits)
            {
                bitBuffer |= hashBytes[byteIndex++] << bitsRemaining;
                bitsRemaining += 8;
            }

            chars[charIndex] = Base32Alphabet[bitBuffer & 31];
            bitBuffer >>= Base32CharBits;
            bitsRemaining -= Base32CharBits;
        }

        return new string(chars);
    }
}
