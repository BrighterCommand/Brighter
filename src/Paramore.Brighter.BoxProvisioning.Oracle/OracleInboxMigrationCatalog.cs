// The MIT License (MIT)
// Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Collections.Generic;
using Paramore.Brighter.Inbox.Oracle;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Defines the migration history for Oracle inbox tables.
/// </summary>
/// <remarks>
/// The Oracle inbox was introduced with the full current column set from the start (V1 =
/// V_latest). V1's <c>UpScript</c> is the full current DDL. The bootstrap detection branch
/// identifies pre-BoxProvisioning Oracle inbox tables (created directly via
/// <see cref="OracleInboxBuilder.GetDDL"/>) as V1 because the V1 <c>LogicalColumns</c> set
/// equals the columns the builder always produced.
/// <para>
/// LogicalColumns use <see cref="System.StringComparer.OrdinalIgnoreCase"/> — Oracle stores
/// column names as uppercase in <c>ALL_TAB_COLUMNS</c> but the comparison must handle
/// PascalCase names from the migration definitions.
/// </para>
/// </remarks>
public class OracleInboxMigrationCatalog : IAmABoxMigrationCatalog
{
    // Oracle inbox was born with all columns present; V1 LogicalColumns = full set.
    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp", "ContextKey"];

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.InBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));
        return OracleInboxBuilder.GetDDL(
            configuration.InBoxTableName,
            configuration.BinaryMessagePayload);
    }

    /// <summary>
    /// Returns all migrations for the Oracle inbox, ordered by version.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list containing V1 (the full column set — born at V_latest).</returns>
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        var table = configuration.InBoxTableName;
        Identifiers.AssertSafe(table, nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table (Oracle born at full column set)",
                UpScript: OracleInboxBuilder.GetDDL(table, configuration.BinaryMessagePayload),
                LogicalColumns: new System.Collections.Generic.HashSet<string>(
                    s_v1Columns, System.StringComparer.OrdinalIgnoreCase))
        ];
    }
}
