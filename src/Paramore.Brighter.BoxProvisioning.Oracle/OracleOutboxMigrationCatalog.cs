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
using Paramore.Brighter.Outbox.Oracle;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Defines the migration history for Oracle outbox tables.
/// </summary>
/// <remarks>
/// The Oracle outbox was introduced with the full current column set from the start (V1 =
/// V_latest). Unlike MySQL whose V1 carried only the March-2019 minimal shape and accumulated
/// columns through V7, the Oracle provider was never shipped with a subset of these columns.
/// <para>
/// V1's <c>UpScript</c> is therefore the full current DDL — the same as <see cref="FreshInstallDdl"/>.
/// The fresh-install fast path (ADR 0057 §3) still sources its DDL from <see cref="FreshInstallDdl"/>
/// so the bootstrap and fresh-install paths are consistent. The detection bootstrap branch
/// identifies pre-BoxProvisioning Oracle tables (created directly via
/// <see cref="OracleOutboxBuilder.GetDDL"/>) as V1 because the V1 <c>LogicalColumns</c> set
/// is a subset of (or equal to) every column the builder ever produced.
/// </para>
/// <para>
/// LogicalColumns use <see cref="System.StringComparer.OrdinalIgnoreCase"/> — Oracle stores
/// column names as uppercase in <c>ALL_TAB_COLUMNS</c> but the comparison must handle
/// PascalCase names from the migration definitions.
/// </para>
/// </remarks>
public class OracleOutboxMigrationCatalog : IAmABoxMigrationCatalog
{
    // Oracle outbox was born with all columns present; V1 LogicalColumns = full set.
    private static readonly string[] s_v1Columns =
    [
        "MessageId", "Topic", "MessageType", "Timestamp",
        "CorrelationId", "ReplyTo", "ContentType", "PartitionKey",
        "WorkflowId", "JobId", "Dispatched", "HeaderBag", "Body",
        "Source", "Type", "DataSchema", "Subject",
        "TraceParent", "TraceState", "Baggage",
        "DataRef", "SpecVersion"
    ];

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.OutBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.OutBoxTableName));
        return OracleOutboxBuilder.GetDDL(
            configuration.OutBoxTableName,
            configuration.BinaryMessagePayload);
    }

    /// <summary>
    /// Returns all migrations for the Oracle outbox, ordered by version.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list containing V1 (the full column set — born at V_latest).</returns>
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        var table = configuration.OutBoxTableName;
        Identifiers.AssertSafe(table, nameof(IAmARelationalDatabaseConfiguration.OutBoxTableName));

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table (Oracle born at full column set)",
                UpScript: OracleOutboxBuilder.GetDDL(table, configuration.BinaryMessagePayload),
                LogicalColumns: new System.Collections.Generic.HashSet<string>(
                    s_v1Columns, System.StringComparer.OrdinalIgnoreCase))
        ];
    }
}
