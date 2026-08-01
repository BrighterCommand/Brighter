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

using System;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning.Legacy;

/// <summary>
/// Seeds a Spanner outbox table at the pre-<c>CausationId</c> (V7) logical version using hand-rolled
/// DDL. Spanner has no migration catalog (it is provisioner-based), so the legacy shape is expressed
/// directly here rather than via a <c>*MigrationCatalog</c> UpScript round-trip. The column set matches
/// the live <c>SpannerOutboxBuilder</c> text DDL minus the <c>CausationId</c> column added by Spec 0027
/// (#2541) — the realistic outbox a user has after upgrading Brighter without the causation migration.
/// No replay index is created (that index ships with <c>CausationId</c>).
/// </summary>
internal static class SpannerOutboxLegacySeeder
{
    // Mirror of SpannerOutboxBuilder.TextOutboxDdl with the `CausationId` column removed.
    private const string V7TextDdl = """
        CREATE TABLE IF NOT EXISTS `{0}`
        (
          `MessageId` STRING(255) NOT NULL,
          `Topic` STRING(255),
          `MessageType` STRING(32),
          `Timestamp` TIMESTAMP,
          `CorrelationId` STRING(255),
          `ReplyTo` STRING(255),
          `ContentType` STRING(128),
          `PartitionKey` STRING(128),
          `Dispatched` TIMESTAMP,
          `HeaderBag` STRING(MAX),
          `Body` STRING(MAX),
          `Source` STRING(255),
          `Type` STRING(255),
          `DataSchema` STRING(255),
          `Subject` STRING(255),
          `TraceParent` STRING(255),
          `TraceState` STRING(255),
          `Baggage` STRING(MAX),
          `WorkflowId` STRING(255),
          `JobId` STRING(255),
          `DataRef` STRING(255),
          `SpecVersion` STRING(10)
        ) PRIMARY KEY (`MessageId`)
        """;

    /// <summary>
    /// Creates an outbox table with the cumulative pre-<c>CausationId</c> column set. Only the
    /// terminal pre-feature version (V7) is meaningful for the backward-compat characterization —
    /// other versions throw, mirroring the catalog seeders' guard rails.
    /// </summary>
    public static void SeedAtV(int version, string connectionString, string tableName)
    {
        if (version != 7)
            throw new ArgumentOutOfRangeException(nameof(version), version,
                "Spanner legacy outbox seeder only models the terminal pre-CausationId shape (V7)");

        using var connection = new SpannerConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = string.Format(V7TextDdl, tableName);
        command.ExecuteNonQuery();
    }
}
