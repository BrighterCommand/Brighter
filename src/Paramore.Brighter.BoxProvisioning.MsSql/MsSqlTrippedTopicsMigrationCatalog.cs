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
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Defines the migration history for the MSSQL tripped topics table. The chain is the single
/// version that creates the table, using the DDL of <see cref="MsSqlTrippedTopicStore.GetDDL"/>
/// so that the store and the provisioner cannot drift apart. The table name and schema belong to
/// the catalog, because <see cref="IAmARelationalDatabaseConfiguration"/> has no trip table property.
/// </summary>
public class MsSqlTrippedTopicsMigrationCatalog : IAmABoxMigrationCatalog
{
    private static readonly string[] s_columns = ["Topic", "TripTime", "PriorTripTime", "EndedAt"];

    private readonly string _tableName;
    private readonly string? _schemaName;

    /// <summary>
    /// Initialises a catalog for one trip table.
    /// </summary>
    /// <param name="tableName">The trip table name; must be a safe SQL identifier.</param>
    /// <param name="schemaName">The schema holding the table, or null for the login's default schema.</param>
    public MsSqlTrippedTopicsMigrationCatalog(string tableName, string? schemaName = null)
    {
        _tableName = tableName;
        _schemaName = schemaName;
    }

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        AssertSafeIdentifiers();
        return MsSqlTrippedTopicStore.GetDDL(_tableName, _schemaName);
    }

    /// <inheritdoc />
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        AssertSafeIdentifiers();
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create tripped topics table",
                UpScript: MsSqlTrippedTopicStore.GetDDL(_tableName, _schemaName),
                LogicalColumns: s_columns)
        ];
    }

    private void AssertSafeIdentifiers()
    {
        Identifiers.AssertSafe(_tableName, "tableName");
        if (_schemaName is not null)
            Identifiers.AssertSafe(_schemaName, "schemaName");
    }
}
