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

using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Provisions a SQLite inbox table. Pre-lock detection and payload-mode validation are owned
/// by the <see cref="SqlBoxProvisioner{TConnection,TTransaction}"/> base; this class supplies
/// only the abstract hooks for the SQLite connection factory and the inbox payload column name,
/// plus a permanent override of <c>EffectiveSchemaName</c> returning <c>null</c> (SQLite has no
/// schema concept per ADR 0057 §6).
/// </summary>
public class SqliteInboxProvisioner : SqlBoxProvisioner<SqliteConnection, SqliteTransaction>
{
    public SqliteInboxProvisioner(
        IAmAVersionDetectingMigrationHelper<SqliteConnection, SqliteTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<SqliteConnection> payloadValidator,
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : base(detectionHelper, catalog, payloadValidator, configuration, migrationRunner, BoxType.Inbox)
    {
    }

    /// <inheritdoc />
    protected override SqliteConnection CreateConnection(string connectionString)
        => new SqliteConnection(connectionString);

    /// <inheritdoc />
    protected override string PayloadColumnName => "CommandBody";

    /// <summary>
    /// SQLite has no schema concept (per ADR 0057 §6); the detection helper and payload
    /// validator receive <c>null</c> at every call site. The runner call inside
    /// <c>ProvisionAsync</c> still propagates <c>_configuration.SchemaName</c> directly, which
    /// the SQLite migration runner accepts and ignores.
    /// </summary>
    protected override string? EffectiveSchemaName => null;
}
