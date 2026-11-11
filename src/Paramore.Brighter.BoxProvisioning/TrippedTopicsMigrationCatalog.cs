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
/// The migration history for a tripped topics table. The chain is the single version that creates
/// the table, so each backend supplies only the DDL of its trip store's <c>GetDDL</c> and the
/// logical column names, and the store and the provisioner cannot drift apart. The table name and
/// schema belong to the catalog, because <see cref="IAmARelationalDatabaseConfiguration"/> has no
/// trip table property.
/// </summary>
public abstract class TrippedTopicsMigrationCatalog : IAmABoxMigrationCatalog
{
    private readonly string _tableName;
    private readonly string? _schemaName;

    /// <summary>
    /// Initialises a catalog for one trip table.
    /// </summary>
    /// <param name="tableName">The trip table name; must be a safe SQL identifier.</param>
    /// <param name="schemaName">The schema holding the table, or null for the backend's default.</param>
    protected TrippedTopicsMigrationCatalog(string tableName, string? schemaName)
    {
        _tableName = tableName;
        _schemaName = schemaName;
    }

    /// <summary>
    /// The logical column names of the trip table, in the case the backend's detection compares them in.
    /// </summary>
    protected abstract IReadOnlyCollection<string> Columns { get; }

    /// <summary>
    /// The DDL that creates the trip table, and its index, if they do not exist.
    /// </summary>
    /// <param name="tableName">The validated trip table name.</param>
    /// <param name="schemaName">The validated schema name, or null.</param>
    protected abstract string CreateTableDdl(string tableName, string? schemaName);

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        AssertSafeIdentifiers();
        return CreateTableDdl(_tableName, _schemaName);
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
                UpScript: CreateTableDdl(_tableName, _schemaName),
                LogicalColumns: Columns)
        ];
    }

    private void AssertSafeIdentifiers()
    {
        Identifiers.AssertSafe(_tableName, "tableName");
        if (_schemaName is not null)
            Identifiers.AssertSafe(_schemaName, "schemaName");
    }
}
