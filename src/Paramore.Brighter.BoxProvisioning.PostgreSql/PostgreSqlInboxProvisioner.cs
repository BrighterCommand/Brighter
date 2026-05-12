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

using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Provisions a PostgreSQL inbox table. Pre-lock detection and payload-mode validation are
/// owned by the <see cref="SqlBoxProvisioner{TConnection,TTransaction}"/> base; this class
/// supplies only the abstract hooks for the PostgreSQL connection factory and the lower-case
/// inbox payload column name.
/// </summary>
public class PostgreSqlInboxProvisioner : SqlBoxProvisioner<NpgsqlConnection, NpgsqlTransaction>
{
    /// <summary>
    /// Canonical ctor — Phase 8.2 of spec 0028. Takes the role-interface dependencies
    /// explicitly so the provisioner does not reach for backend statics.
    /// </summary>
    public PostgreSqlInboxProvisioner(
        IAmAVersionDetectingMigrationHelper<NpgsqlConnection, NpgsqlTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<NpgsqlConnection> payloadValidator,
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : base(detectionHelper, catalog, payloadValidator, configuration, migrationRunner, BoxType.Inbox)
    {
    }

    /// <summary>
    /// Backward-compatible ctor preserving the spec 0027 public surface — used by existing
    /// call-sites (extensions + integration tests). Synthesises default singletons for the
    /// three role-interface dependencies; removed when the DI cascade lands in Phase 9.
    /// </summary>
    public PostgreSqlInboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : this(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlInboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            configuration,
            migrationRunner)
    {
    }

    /// <inheritdoc />
    protected override NpgsqlConnection CreateConnection(string connectionString)
        => new NpgsqlConnection(connectionString);

    /// <inheritdoc />
    protected override string PayloadColumnName => "commandbody";
}
