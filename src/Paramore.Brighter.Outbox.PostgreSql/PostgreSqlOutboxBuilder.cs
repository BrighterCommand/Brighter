#region Licence
/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter.Outbox.PostgreSql
{
    /// <summary>
    /// Provide SQL statement helpers for creation of an Outbox
    /// </summary>
    public class PostgreSqlOutboxBuilder
    {
        private const string TextOutboxDdl =
            """
            CREATE TABLE IF NOT EXISTS {0}
            (
                Id bigserial PRIMARY KEY,
                MessageId character varying(255) UNIQUE NOT NULL,
                Topic character varying(255) NULL,
                MessageType character varying(32) NULL,
                Timestamp timestamptz NULL,
                CorrelationId character varying(255) NULL,
                ReplyTo character varying(255) NULL,
                ContentType character varying(128) NULL,
                PartitionKey character varying(128) NULL,  
                WorkflowId character varying(255) NULL,
                JobId character varying(255) NULL,
                Dispatched timestamptz NULL,
                HeaderBag text NULL,
                Body text NULL,
                Source character varying (255) NULL,
                Type character varying (255) NULL,
                DataSchema character varying (255) NULL,
                Subject character varying (255) NULL,
                TraceParent character varying (255) NULL,
                TraceState character varying (255) NULL,
                Baggage text NULL,
                DataRef character varying (255) NULL,
                SpecVersion character varying (255) NULL
            );
            """;

        private const string BinaryOutboxDdl =
            """
            CREATE TABLE {0}
            (
                Id bigserial PRIMARY KEY,
                MessageId character varying(255) UNIQUE NOT NULL,
                Topic character varying(255) NULL,
                MessageType character varying(32) NULL,
                Timestamp timestamptz NULL,
                CorrelationId character varying(255) NULL,
                ReplyTo character varying(255) NULL,
                ContentType character varying(128) NULL,
                PartitionKey character varying(128) NULL,  
                WorkflowId character varying(255) NULL,
                JobId character varying(255) NULL,
                Dispatched timestamptz NULL,
                HeaderBag text NULL,
                Body bytea NULL,
                Source character varying (255) NULL,
                Type character varying (255) NULL,
                DataSchema character varying (255) NULL,
                Subject character varying (255) NULL,
                TraceParent character varying (255) NULL,
                TraceState character varying (255) NULL,
                Baggage text NULL,
                DataRef character varying (255) NULL,
                SpecVersion character varying (255) NULL
            );
            """;

        private const string OutboxExistsSQL = @"SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_CATALOG = '{0}' AND TABLE_NAME = '{1}')";

        /// <summary>
        /// Get the DDL required to create the Outbox in Postgres
        /// </summary>
        /// <param name="outboxTableName">The name you want to use for the table</param>
        /// <param name="binaryMessagePayload"></param>
        /// <param name="schemaName">
        /// Optional Postgres schema name. When non-null, the emitted DDL schema-qualifies the
        /// table as <c>{schemaName}.{outboxTableName}</c> (unquoted, matching the existing
        /// V2..V7 ALTER convention in PostgreSqlOutboxMigrationCatalog); otherwise the table
        /// is emitted unqualified and lands in the connection's search_path default (typically
        /// <c>public</c>). Per PR #4039 reviewer item M4-1 (F1b): callers configuring
        /// <see cref="Paramore.Brighter.IAmARelationalDatabaseConfiguration.SchemaName"/> rely
        /// on the table actually landing in that schema, which the unqualified form cannot
        /// guarantee. PG ADR 0057 §1 requires lowercase identifiers; the catalog enforces
        /// this via <c>Identifiers.AssertSafe</c> before reaching this builder.
        /// </param>
        /// <returns>The required DDL</returns>
        public static string GetDDL(string outboxTableName, bool binaryMessagePayload = false, string? schemaName = null)
        {
            var qualifiedTable = schemaName is null
                ? outboxTableName
                : $"{schemaName}.{outboxTableName}";
            return binaryMessagePayload ? string.Format(BinaryOutboxDdl, qualifiedTable) : string.Format(TextOutboxDdl, qualifiedTable);
        }

        /// <summary>
        /// Get the SQL statements required to test for the existence of an Inbox in Postgres
        /// </summary>
        /// <param name="tableCatalog">The Postgres table catalog that the table is in</param>
        /// <param name="outboxTableName">The name that was used for the Inbox table</param>
        /// <returns>The required SQL</returns>
        public static string GetExistsQuery(string tableCatalog, string outboxTableName)
        {
            return string.Format(OutboxExistsSQL, tableCatalog, outboxTableName);
        }
    }
}
