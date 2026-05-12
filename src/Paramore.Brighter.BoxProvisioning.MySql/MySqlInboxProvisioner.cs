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

using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Provisions a MySQL inbox table. Pre-lock detection and payload-mode validation are owned
/// by the <see cref="SqlBoxProvisioner{TConnection,TTransaction}"/> base; this class supplies
/// only the abstract hooks for the MySQL connection factory and the inbox payload column name.
/// </summary>
public class MySqlInboxProvisioner : SqlBoxProvisioner<MySqlConnection, MySqlTransaction>
{
    /// <summary>
    /// Canonical ctor — Phase 8.3 of spec 0028. Takes the role-interface dependencies
    /// explicitly so the provisioner does not reach for backend statics.
    /// </summary>
    public MySqlInboxProvisioner(
        IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<MySqlConnection> payloadValidator,
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
    public MySqlInboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : this(
            new MySqlBoxDetectionHelper(),
            new MySqlInboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            configuration,
            migrationRunner)
    {
    }

    /// <inheritdoc />
    protected override MySqlConnection CreateConnection(string connectionString)
        => new MySqlConnection(connectionString);

    /// <inheritdoc />
    protected override string PayloadColumnName => "CommandBody";
}
