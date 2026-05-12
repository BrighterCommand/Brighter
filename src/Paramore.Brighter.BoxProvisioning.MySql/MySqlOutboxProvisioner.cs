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
/// Provisions a MySQL outbox table. Pre-lock detection and payload-mode validation are owned
/// by the <see cref="SqlBoxProvisioner{TConnection,TTransaction}"/> base; this class supplies
/// only the abstract hooks for the MySQL connection factory and the outbox payload column name,
/// plus a transitional identity override of <c>ClampDetectedVersion</c> to preserve MySQL's
/// no-clamp pre-13.B behaviour (removed in Phase 13.B per F11).
/// </summary>
public class MySqlOutboxProvisioner : SqlBoxProvisioner<MySqlConnection, MySqlTransaction>
{
    /// <summary>
    /// Canonical ctor — Phase 8.3 of spec 0028. Takes the role-interface dependencies
    /// explicitly so the provisioner does not reach for backend statics.
    /// </summary>
    public MySqlOutboxProvisioner(
        IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<MySqlConnection> payloadValidator,
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
    public MySqlOutboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : this(
            new MySqlBoxDetectionHelper(),
            new MySqlOutboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            configuration,
            migrationRunner)
    {
    }

    /// <inheritdoc />
    protected override MySqlConnection CreateConnection(string connectionString)
        => new MySqlConnection(connectionString);

    /// <inheritdoc />
    protected override string PayloadColumnName => "Body";

    /// <summary>
    /// <b>TRANSITIONAL — removed in Phase 13.B per F11.</b> Identity override preserves
    /// MySQL's no-clamp pre-13.A behaviour bit-for-bit (NF9 — behavioural neutrality of
    /// the sub-phase A structural pull-up). Phase 13.B (F11) unifies MySQL's behaviour
    /// with the other three relational backends and removes both this override AND the
    /// base hook in the same commit, inlining the clamp into <c>DetectTableStateAsync</c>.
    /// See ADR 0058 §B.5 line 646 and the base's <c>ClampDetectedVersion</c> XML-doc.
    /// </summary>
    /// <param name="detectedVersion">The version inferred by the detection helper for a
    /// pre-existing table without history rows.</param>
    /// <returns>The detected version unchanged — MySQL's pre-13.B contract.</returns>
    protected override int ClampDetectedVersion(int detectedVersion) => detectedVersion;
}
