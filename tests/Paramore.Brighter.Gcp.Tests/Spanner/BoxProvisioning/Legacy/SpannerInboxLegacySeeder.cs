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

using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning.Legacy;

/// <summary>
/// Seeds a Spanner inbox table at the pre-<c>CausationId</c> (V2) logical version using hand-rolled
/// DDL. Spanner has no migration catalog (it is provisioner-based), so unlike the four catalog
/// backends there is no <c>*MigrationCatalog</c> to drive a UpScript round-trip — the legacy shape is
/// expressed directly here. The column set matches the live <c>SpannerInboxBuilder</c> DDL minus the
/// <c>CausationId</c> column added by Spec 0027 (#2541). <c>ContextKey</c> is part of the primary key,
/// so it is always present — the pre-Replay shape a user runs after upgrading Brighter without the
/// causation migration.
/// </summary>
internal static class SpannerInboxLegacySeeder
{
    // Mirror of SpannerInboxBuilder.InboxDDL with the trailing `CausationId` column removed.
    private const string V2Ddl = """
        CREATE TABLE IF NOT EXISTS `{0}`(
            `CommandId` STRING(256) NOT NULL,
            `CommandType` STRING(256),
            `CommandBody` JSON,
            `Timestamp` TIMESTAMP,
            `ContextKey` STRING(256)
        ) PRIMARY KEY (`CommandId`, `ContextKey`)
        """;

    /// <summary>
    /// Creates an inbox table with the V2 column set (<c>ContextKey</c> present, but no
    /// <c>CausationId</c>) — the pre-Replay schema a user runs after upgrading Brighter
    /// without applying the causation migration.
    /// </summary>
    public static void SeedAtV2(string connectionString, string tableName)
    {
        using var connection = new SpannerConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = string.Format(V2Ddl, tableName);
        command.ExecuteNonQuery();
    }
}
