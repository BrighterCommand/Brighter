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

using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Provisions a MSSQL outbox table. Pre-lock detection and payload-mode validation are owned
/// by the <see cref="SqlBoxProvisioner{TConnection,TTransaction}"/> base; this class supplies
/// only the abstract hooks for the MSSQL connection factory and the outbox payload column name.
/// </summary>
public class MsSqlOutboxProvisioner : SqlBoxProvisioner<SqlConnection, SqlTransaction>
{
    /// <summary>
    /// Canonical ctor — Phase 8.1 of spec 0028. Takes the role-interface dependencies
    /// explicitly so the provisioner does not reach for backend statics.
    /// </summary>
    public MsSqlOutboxProvisioner(
        IAmAVersionDetectingMigrationHelper<SqlConnection, SqlTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<SqlConnection> payloadValidator,
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : base(detectionHelper, catalog, payloadValidator, configuration, migrationRunner, BoxType.Outbox)
    {
    }

    /// <summary>
    /// Backward-compatible ctor preserving the spec 0027 public surface — used by existing
    /// call-sites (extensions + integration tests). Synthesises default singletons for the
    /// three role-interface dependencies; removed when the DI cascade lands in Phase 9.
    /// </summary>
    public MsSqlOutboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : this(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            configuration,
            migrationRunner)
    {
    }

    /// <inheritdoc />
    protected override SqlConnection CreateConnection(string connectionString)
        => new SqlConnection(connectionString);

    /// <inheritdoc />
    protected override string PayloadColumnName => "Body";
}
