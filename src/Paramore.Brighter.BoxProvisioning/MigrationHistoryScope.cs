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

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Controls where the box migration-history table (<c>__BrighterMigrationHistory</c>) is
/// physically placed relative to the configured
/// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/>.
/// </summary>
/// <remarks>
/// Spec 0029 / ADR 0060. The value is supplied through
/// <c>BoxProvisioningOptions.MigrationHistoryScope</c> and threaded into each per-backend
/// migration runner. The default <see cref="Global"/> is byte-for-byte today's behaviour — no
/// operator action is required to upgrade.
/// <para>
/// <b>Backend support.</b> Only MSSQL and PostgreSQL honour <see cref="PerSchema"/> placement;
/// MySQL (where schema == database), SQLite (no schema concept) and Spanner (degenerate
/// fresh-install-only model per ADR 0057 §6) treat <see cref="PerSchema"/> as a no-op and keep
/// history in their default location. No exception is thrown so a single
/// <c>BoxProvisioningOptions</c> can target a mixed backend set without per-backend branching;
/// the placement-decision log emitted by the runner each run (see <c>SqlBoxMigrationRunner</c>'s
/// per-run Information log) surfaces the resolved schema so operators can confirm where their
/// history landed without inspecting the database.
/// </para>
/// <para>
/// <b>Flip semantics.</b> A <see cref="Global"/>→<see cref="PerSchema"/> flip on MSSQL/PG
/// auto-seeds the per-schema history table from the legacy default-schema history (ADR 0060 D5),
/// so existing migrations are not re-applied. The seed runs under the same advisory lock and
/// transaction as the CREATE and copies only this tenant's rows
/// (<c>WHERE SchemaName=@schemaName AND BoxTableName=@boxTableName</c>) with a
/// <c>NOT EXISTS</c> primary-key guard so a repeated flip is idempotent. The seed executes on
/// <b>every</b> PerSchema provision (so a second box-type flipping after the first still gets
/// seeded); the NOT EXISTS guard makes steady-state runs a zero-row no-op. The seed requires
/// <b>read access to the legacy default-schema history table</b> (<c>dbo.__BrighterMigrationHistory</c>
/// on MSSQL, <c>public.__BrighterMigrationHistory</c> on PG) for the lifetime of the PerSchema
/// deployment, not just the first flip — operators who revoke <c>SELECT</c> after the initial
/// flip will hit a <see cref="ConfigurationException"/> on every subsequent provision run, with
/// the inner provider exception attached. The reverse flip (<see cref="PerSchema"/>→<see cref="Global"/>)
/// and post-flip cleanup of the legacy rows are <b>out of scope</b>; operators wanting to
/// reclaim that storage must run their own ad-hoc DELETE against the legacy table.
/// </para>
/// <para>
/// <b>Misconfiguration.</b> Selecting <see cref="PerSchema"/> on a placement backend with a
/// <c>null</c> <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/> throws
/// <see cref="ConfigurationException"/> at the entry to the runner — there is no schema to
/// place history in. Per-tenant identifiers are validated through <c>Identifiers.AssertSafe</c>
/// before any DDL is emitted.
/// </para>
/// </remarks>
public enum MigrationHistoryScope
{
    /// <summary>
    /// History lives in the backend default schema (MSSQL <c>dbo</c> / PostgreSQL <c>public</c> /
    /// the connection-bound <c>DATABASE()</c> on MySQL). This is the default and is identical to
    /// the behaviour prior to this feature — even for operators who configure a non-null
    /// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/>.
    /// </summary>
    Global = 0,

    /// <summary>
    /// On MSSQL and PostgreSQL, history is created in the configured
    /// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/>, co-located with the tenant's
    /// box tables inside its isolation/backup boundary. It is a no-op on backends without a
    /// distinct schema concept (MySQL, SQLite, Spanner), where history stays in the default
    /// location and no exception is thrown.
    /// </summary>
    /// <remarks>
    /// See the enum's top-level remarks for flip semantics (auto-seed on
    /// <see cref="Global"/>→<see cref="PerSchema"/>, idempotent re-runs, reverse flip out of
    /// scope) and the read-access requirement on the legacy default-schema history table.
    /// </remarks>
    PerSchema = 1
}
