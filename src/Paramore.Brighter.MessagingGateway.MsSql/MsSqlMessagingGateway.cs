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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MessagingGateway.MsSql;

/// <summary>
/// Base class for MS SQL messaging gateway components, providing the queue store provisioning
/// that both the consumer and the producer side need.
/// </summary>
/// <remarks>
/// The queue table is always resolved through <c>SCHEMA_NAME()</c> — the caller's default schema —
/// and never through <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/>. That is not an
/// oversight: <c>MsSqlMessageQueue</c> emits an unqualified <c>[{QueueStoreTable}]</c> in every
/// statement it issues, so the runtime resolves through the default schema too. Honouring
/// <c>SchemaName</c> here would create the table somewhere the gateway never looks. The property
/// still applies to the Outbox and the Inbox, which do qualify their SQL.
/// </remarks>
public class MsSqlMessagingGateway(IAmARelationalDatabaseConfiguration configuration)
{
    // SQL Server: "there is already an object named ...", and its index equivalent.
    private const int ObjectAlreadyExists = 2714;
    private const int IndexAlreadyExists = 1913;
    private const int DeadlockVictim = 1205;

    private const int MaxIdentifierLength = 128;

    // The topic index is named after the table — MsSqlQueueBuilder.GetIndexDDL emits
    // [IX_{table}_Topic] — so the table name's real ceiling on the Create path is nine characters
    // lower than SQL Server's own. A 120 character table name is created and then its index fails
    // with error 103, measured: "The identifier that starts with ... is too long."
    private const string IndexNamePrefix = "IX_";
    private const string IndexNameSuffix = "_Topic";
    private static readonly int s_maxCreatableQueueTableLength =
        MaxIdentifierLength - IndexNamePrefix.Length - IndexNameSuffix.Length;

    // The queue table is configuration-level: nothing in this assembly varies it per publication or
    // per subscription, so ensuring it once per gateway instance is enough. Without this a producer
    // factory holding twenty publications opens twenty connections and runs twenty identical no-op
    // DDL batches as the host starts, and a dispatcher repeats the probe once per performer.
    private readonly ConcurrentDictionary<OnMissingChannel, bool> _ensured = new();

    // Written once each and used by both the IF NOT EXISTS guard and the post-create re-probe: if
    // the two ever drifted apart, the re-probe would throw against a table it had just created.
    private const string TablePredicate = """
                                          SELECT 1 FROM sys.tables t
                                          INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                                          WHERE t.name = @queueTable AND s.name = SCHEMA_NAME()
                                          """;

    private const string TopicIndexPredicate = """
                                               SELECT 1 FROM sys.indexes i
                                               INNER JOIN sys.index_columns ic
                                                   ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                                               INNER JOIN sys.columns c
                                                   ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                                               WHERE i.object_id = OBJECT_ID(QUOTENAME(SCHEMA_NAME()) + '.' + QUOTENAME(@queueTable))
                                                 AND i.is_primary_key = 0
                                                 AND ic.key_ordinal = 1
                                                 AND c.name = 'Topic'
                                               """;

    /// <summary>
    /// Gets the configuration describing the database and the queue store table.
    /// </summary>
    protected IAmARelationalDatabaseConfiguration Configuration { get; } =
        configuration ?? throw new ArgumentNullException(nameof(configuration));

    /// <summary>
    /// Ensures the queue store table and its topic index exist, according to
    /// <paramref name="makeChannels"/>. Safe to run on every start, and safe to run concurrently.
    /// </summary>
    /// <param name="makeChannels">
    /// <see cref="OnMissingChannel.Assume"/> does nothing at all and opens no connection;
    /// <see cref="OnMissingChannel.Create"/> creates the table and index if absent;
    /// <see cref="OnMissingChannel.Validate"/> checks and throws if the table is missing.
    /// </param>
    /// <exception cref="ConfigurationException">
    /// Thrown when the queue store table is not a plain SQL identifier, when the database cannot be
    /// reached, or when <paramref name="makeChannels"/> is <see cref="OnMissingChannel.Validate"/>
    /// and the table does not exist.
    /// </exception>
    protected void EnsureQueueStoreExists(OnMissingChannel makeChannels)
    {
        if (makeChannels == OnMissingChannel.Assume) return;
        if (_ensured.ContainsKey(makeChannels)) return;

        var queueTable = ValidatedQueueTableName(makeChannels);
        using var connection = Connect(queueTable);

        if (makeChannels == OnMissingChannel.Validate)
        {
            if (!Exists(connection, TablePredicate, queueTable))
                throw new ConfigurationException(
                    $"The queue store table '{queueTable}' does not exist in the default schema of " +
                    $"database '{Configuration.DatabaseName}'.");

            _ensured[makeChannels] = true;
            return;
        }

        CreateTable(connection, queueTable);

        // The 2714 the create may have swallowed means "an object of that name exists", not "the
        // table exists" — a view or a procedure holding the name would look identical. Re-probe so
        // a name collision stops resembling a lost race, and fails here rather than deep inside
        // the gateway on the first send.
        if (!Exists(connection, TablePredicate, queueTable))
            throw new ConfigurationException(
                $"'{queueTable}' was not created and does not exist as a table. Something else in " +
                "this database owns that name.");

        CreateIndex(connection, queueTable);

        // The same hazard: 1913 means "an index of that name exists", while the guard asks about
        // the leading column. An index named IX_..._Topic over some other column satisfies neither.
        if (!Exists(connection, TopicIndexPredicate, queueTable))
            throw new ConfigurationException(
                $"No index leading on Topic exists for '{queueTable}', and one could not be " +
                "created. An index of that name over different columns already exists.");

        _ensured[makeChannels] = true;
    }

    /// <summary>
    /// Asynchronously ensures the queue store table and its topic index exist, according to
    /// <paramref name="makeChannels"/>. Safe to run on every start, and safe to run concurrently.
    /// </summary>
    /// <param name="makeChannels">See <see cref="EnsureQueueStoreExists"/>.</param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <exception cref="ConfigurationException">See <see cref="EnsureQueueStoreExists"/>.</exception>
    protected async Task EnsureQueueStoreExistsAsync(
        OnMissingChannel makeChannels, CancellationToken cancellationToken = default)
    {
        if (makeChannels == OnMissingChannel.Assume) return;
        if (_ensured.ContainsKey(makeChannels)) return;

        var queueTable = ValidatedQueueTableName(makeChannels);
        // Not `await using`: this package targets net462, where SqlConnection does not implement
        // IAsyncDisposable, and the async using is a compile error there (CS8417).
        using var connection = await ConnectAsync(queueTable, cancellationToken);

        if (makeChannels == OnMissingChannel.Validate)
        {
            if (!await ExistsAsync(connection, TablePredicate, queueTable, cancellationToken))
                throw new ConfigurationException(
                    $"The queue store table '{queueTable}' does not exist in the default schema of " +
                    $"database '{Configuration.DatabaseName}'.");

            _ensured[makeChannels] = true;
            return;
        }

        await CreateTableAsync(connection, queueTable, cancellationToken);

        // The 2714 the create may have swallowed means "an object of that name exists", not "the
        // table exists" — a view or a procedure holding the name would look identical. Re-probe so
        // a name collision stops resembling a lost race, and fails here rather than deep inside
        // the gateway on the first send.
        if (!await ExistsAsync(connection, TablePredicate, queueTable, cancellationToken))
            throw new ConfigurationException(
                $"'{queueTable}' was not created and does not exist as a table. Something else in " +
                "this database owns that name.");

        await CreateIndexAsync(connection, queueTable, cancellationToken);

        // The same hazard: 1913 means "an index of that name exists", while the guard asks about
        // the leading column. An index named IX_..._Topic over some other column satisfies neither.
        if (!await ExistsAsync(connection, TopicIndexPredicate, queueTable, cancellationToken))
            throw new ConfigurationException(
                $"No index leading on Topic exists for '{queueTable}', and one could not be " +
                "created. An index of that name over different columns already exists.");

        _ensured[makeChannels] = true;
    }

    // GetDDL formats the name into CREATE TABLE [{0}] and escapes nothing, so the one character
    // that matters is ']' — it closes the bracket and everything after it is free SQL. Everything
    // else a bracketed identifier legally holds is allowed through, hyphens and spaces and dots
    // included: queue tables are routinely named after a GUID, and a stricter rule here would
    // reject names this gateway has always accepted.
    private string ValidatedQueueTableName(OnMissingChannel makeChannels)
    {
        var queueTable = Configuration.QueueStoreTable;

        if (string.IsNullOrWhiteSpace(queueTable))
            throw new ConfigurationException("The queue store table name is missing.");

        if (queueTable.IndexOf(']') >= 0)
            throw new ConfigurationException(
                $"The queue store table '{queueTable}' contains ']', which would close the bracket " +
                "in the CREATE TABLE statement used to provision it.");

        // SQL Server's own limit for a regular identifier. Without the bound an over-long name
        // reaches the CREATE and fails there with a message that does not mention configuration.
        if (queueTable.Length > MaxIdentifierLength)
            throw new ConfigurationException(
                $"The queue store table name is {queueTable.Length} characters; SQL Server allows " +
                $"at most {MaxIdentifierLength}.");

        // The tighter bound belongs to Create alone. Validate runs one parameterised probe and
        // builds no identifier, so a table of 120-128 characters that already exists is a table
        // this gateway can legitimately be pointed at; only creating its index is out of reach.
        if (makeChannels == OnMissingChannel.Create && queueTable.Length > s_maxCreatableQueueTableLength)
            throw new ConfigurationException(
                $"The queue store table name is {queueTable.Length} characters. Provisioning also " +
                $"creates the index '{IndexNamePrefix}{queueTable}{IndexNameSuffix}', which is " +
                $"{queueTable.Length + IndexNamePrefix.Length + IndexNameSuffix.Length} characters " +
                $"and over SQL Server's {MaxIdentifierLength} character limit, so the name must be " +
                $"at most {s_maxCreatableQueueTableLength} characters to be created here. A table " +
                "of this name that already exists can still be used with MakeChannels = Validate.");

        return queueTable;
    }

    // Only the connect is wrapped, and deliberately: this runs as the host starts, so the commonest
    // first-run mistakes — wrong server, malformed string, no permission — would otherwise surface
    // as a bare provider exception with nothing saying which table we were provisioning.
    private SqlConnection Connect(string queueTable)
    {
        try
        {
            var connection = new SqlConnection(Configuration.ConnectionString);
            connection.Open();
            return connection;
        }
        catch (Exception ex) when (ex is SqlException or ArgumentException or InvalidOperationException)
        {
            throw new ConfigurationException(
                $"Could not connect in order to provision the queue store '{queueTable}'. The " +
                $"provider said: {ex.Message}", ex);
        }
    }

    private async Task<SqlConnection> ConnectAsync(string queueTable, CancellationToken cancellationToken)
    {
        try
        {
            var connection = new SqlConnection(Configuration.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (Exception ex) when (ex is SqlException or ArgumentException or InvalidOperationException)
        {
            throw new ConfigurationException(
                $"Could not connect in order to provision the queue store '{queueTable}'. The " +
                $"provider said: {ex.Message}", ex);
        }
    }

    private static bool Exists(SqlConnection connection, string predicate, string queueTable)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM ({predicate}) AS found(one);";
        command.Parameters.AddWithValue("@queueTable", queueTable);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static async Task<bool> ExistsAsync(
        SqlConnection connection, string predicate, string queueTable, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM ({predicate}) AS found(one);";
        command.Parameters.AddWithValue("@queueTable", queueTable);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    // Two commands rather than one batch: a swallowed error abandons the rest of its batch, so a
    // table create that loses the race would take the index statement down with it.
    private static void CreateTable(SqlConnection connection, string queueTable) =>
        Execute(connection, TableSql(queueTable), ObjectAlreadyExists, queueTable);

    private static Task CreateTableAsync(
        SqlConnection connection, string queueTable, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, TableSql(queueTable), ObjectAlreadyExists, queueTable, cancellationToken);

    // Guarded on what the index IS, not what it is called: any index whose leading column is Topic
    // on this table. A name guard would duplicate the convention MsSqlQueueBuilder owns, and would
    // silently stop matching if that convention ever moved.
    private static void CreateIndex(SqlConnection connection, string queueTable) =>
        Execute(connection, IndexSql(queueTable), IndexAlreadyExists, queueTable);

    private static Task CreateIndexAsync(
        SqlConnection connection, string queueTable, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, IndexSql(queueTable), IndexAlreadyExists, queueTable, cancellationToken);

    private static string TableSql(string queueTable) =>
        $"""
         IF NOT EXISTS ({TablePredicate})
         BEGIN
             {MsSqlQueueBuilder.GetDDL(queueTable)}
         END;
         """;

    private static string IndexSql(string queueTable) =>
        $"""
         IF NOT EXISTS ({TopicIndexPredicate})
         BEGIN
             {MsSqlQueueBuilder.GetIndexDDL(queueTable)}
         END;
         """;

    // Instances starting together can both pass a guard and race to create. Box Provisioning takes
    // an advisory lock; there is no equivalent for the queue, so the loser treats "already exists"
    // as the outcome it wanted. A deadlock victim is retried once — concurrent DDL surfaces as 1205
    // as readily as 2714 or 1913 — and a second 1205 propagates, because a database deadlocking
    // twice on one CREATE is not a race any more.
    private static void Execute(SqlConnection connection, string sql, int alreadyExists, string queueTable)
    {
        try
        {
            ExecuteOnce(connection, sql, queueTable);
        }
        catch (SqlException ex) when (ex.Number == DeadlockVictim)
        {
            try
            {
                ExecuteOnce(connection, sql, queueTable);
            }
            catch (SqlException retry) when (retry.Number == alreadyExists)
            {
                // The process we deadlocked with created it first.
            }
            catch (SqlException retry) when (retry.Number != DeadlockVictim)
            {
                throw CouldNotProvision(retry, queueTable);
            }
        }
        catch (SqlException ex) when (ex.Number == alreadyExists)
        {
            // Lost the create race; the other process made it, which is the outcome we wanted.
        }
        catch (SqlException ex)
        {
            throw CouldNotProvision(ex, queueTable);
        }
    }

    private static async Task ExecuteAsync(
        SqlConnection connection, string sql, int alreadyExists, string queueTable,
        CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteOnceAsync(connection, sql, queueTable, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == DeadlockVictim)
        {
            try
            {
                await ExecuteOnceAsync(connection, sql, queueTable, cancellationToken);
            }
            catch (SqlException retry) when (retry.Number == alreadyExists)
            {
                // The process we deadlocked with created it first.
            }
            catch (SqlException retry) when (retry.Number != DeadlockVictim)
            {
                throw CouldNotProvision(retry, queueTable);
            }
        }
        catch (SqlException ex) when (ex.Number == alreadyExists)
        {
            // Lost the create race; the other process made it, which is the outcome we wanted.
        }
        catch (SqlException ex)
        {
            throw CouldNotProvision(ex, queueTable);
        }
    }

    // Create is the default, so the commonest way to meet this code is by upgrading into it: an
    // application whose login has DML rights only gets error 262, "CREATE TABLE permission denied",
    // thrown out of channel open or producer construction with nothing in it naming MakeChannels.
    // Measured against a db_datareader + db_datawriter login; 262 is not 2714, 1913 or 1205, so
    // without this it propagates raw.
    private static ConfigurationException CouldNotProvision(SqlException ex, string queueTable) =>
        new($"Could not provision the queue store '{queueTable}'. The provider said: {ex.Message} " +
            "If this database is provisioned elsewhere — a migration, a DBA, an application login " +
            "with no DDL rights — then set MakeChannels to Validate to check the table exists " +
            "instead of creating it, or to Assume to skip the check entirely.", ex);

    // The parameter is built here rather than passed in, because SqlCommand.Dispose does not detach
    // parameters: a retry that re-used the instance would throw ArgumentException — "the
    // SqlParameter is already contained by another SqlParameterCollection" — out of the one path
    // that exists to survive a race.
    private static void ExecuteOnce(SqlConnection connection, string sql, string queueTable)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@queueTable", queueTable);
        command.ExecuteNonQuery();
    }

    private static async Task ExecuteOnceAsync(
        SqlConnection connection, string sql, string queueTable, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@queueTable", queueTable);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
