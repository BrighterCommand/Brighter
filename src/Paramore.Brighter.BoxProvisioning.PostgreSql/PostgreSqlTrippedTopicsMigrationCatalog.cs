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
using Paramore.Brighter.Outbox.PostgreSql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Defines the migration history for the PostgreSql tripped topics table, using the DDL of
/// <see cref="PostgreSqlTrippedTopicStore.GetDDL"/> so that the store and the provisioner cannot drift apart.
/// </summary>
/// <remarks>
/// <see cref="IAmABoxMigration.LogicalColumns"/> are lowercase, the case PostgreSQL folds column names to.
/// </remarks>
public class PostgreSqlTrippedTopicsMigrationCatalog : TrippedTopicsMigrationCatalog
{
    private static readonly string[] s_columns = ["topic", "trip_time", "prior_trip_time", "ended_at"];

    /// <summary>
    /// Initialises a catalog for one trip table.
    /// </summary>
    /// <param name="tableName">The trip table name; must be a safe SQL identifier.</param>
    /// <param name="schemaName">The schema holding the table, or null for the search path's default.</param>
    public PostgreSqlTrippedTopicsMigrationCatalog(string tableName, string? schemaName = null)
        : base(tableName, schemaName)
    {
    }

    /// <inheritdoc />
    protected override IReadOnlyCollection<string> Columns => s_columns;

    /// <inheritdoc />
    protected override string CreateTableDdl(string tableName, string? schemaName) =>
        PostgreSqlTrippedTopicStore.GetDDL(tableName, schemaName);
}
