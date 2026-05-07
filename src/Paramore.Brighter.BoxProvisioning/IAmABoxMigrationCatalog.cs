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

using System.Collections.Generic;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// The migration-catalogue role for a BoxProvisioning backend. Implementations
/// know the ordered chain of migrations for one (backend, box-type) pairing
/// — e.g. "the V1..V7 chain for the MSSQL outbox" or "the V1..V2 chain for
/// the Postgres inbox".
/// </summary>
/// <remarks>
/// One implementation per (backend, box-type) — eight classes total across the
/// four relational backends (MSSQL, Postgres, MySQL, SQLite).
/// <para>
/// Spanner is exempt per ADR 0057 §6. Spanner's degenerate fresh-install-only
/// model has no V_k chain — the Spanner runner ignores the migrations parameter
/// and no Spanner catalogue is shipped.
/// </para>
/// <para>
/// Implementations are stateless services and are safe to register as DI singletons.
/// The configuration is supplied per call (rather than per ctor) because some
/// migrations interpolate values from the configuration into their script bodies.
/// </para>
/// </remarks>
public interface IAmABoxMigrationCatalog
{
    /// <summary>
    /// Returns the migration chain in monotonic version order. The list is consumed
    /// by the migration runner and by
    /// <see cref="IAmAVersionDetectingMigrationHelper{TConnection,TTransaction}.DetectCurrentVersionAsync"/>.
    /// </summary>
    /// <param name="configuration">The relational database configuration. Some migrations
    /// interpolate values from configuration (e.g. table names, payload-mode flags) into
    /// their generated SQL scripts.</param>
    IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration);
}
