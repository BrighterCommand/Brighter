#region Licence

/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian.cooper@yahoo.co.uk>

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

using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Inbox.Postgres
{
    /// <summary>
    /// Provide SQL statement helpers for creation of an Inbox
    /// </summary>
    public class PostgreSqlInboxBuilder
    {
        private const string TextOutboxDDL =
            """
            CREATE TABLE IF NOT EXISTS {0}
            (
                CommandId VARCHAR(256) NOT NULL ,
                CommandType VARCHAR(256) NULL ,
                CommandBody TEXT NULL ,
                Timestamp timestamptz  NULL ,
                ContextKey VARCHAR(256) NULL,
                CausationId VARCHAR(256) NULL,
                PRIMARY KEY (CommandId, ContextKey)
            );
            """;

        private const string BinaryOutboxDDL =
            """
            CREATE TABLE IF NOT EXISTS {0}
            (
                CommandId VARCHAR(256) NOT NULL ,
                CommandType VARCHAR(256) NULL ,
                CommandBody BYTEA NULL ,
                Timestamp timestamptz  NULL ,
                ContextKey VARCHAR(256) NULL,
                CausationId VARCHAR(256) NULL,
                PRIMARY KEY (CommandId, ContextKey)
            );
            """;
        
        private const string JsonOutboxDDL =
            """
            CREATE TABLE IF NOT EXISTS {0}
            (
                CommandId VARCHAR(256) NOT NULL ,
                CommandType VARCHAR(256) NULL ,
                CommandBody JSON NULL ,
                Timestamp timestamptz  NULL ,
                ContextKey VARCHAR(256) NULL,
                CausationId VARCHAR(256) NULL,
                PRIMARY KEY (CommandId, ContextKey)
            );
            """;

        private const string JsonbOutboxDDL =
            """
            CREATE TABLE IF NOT EXISTS {0}
            (
                CommandId VARCHAR(256) NOT NULL ,
                CommandType VARCHAR(256) NULL ,
                CommandBody JSONB NULL ,
                Timestamp timestamptz  NULL ,
                ContextKey VARCHAR(256) NULL,
                CausationId VARCHAR(256) NULL,
                PRIMARY KEY (CommandId, ContextKey)
            );
            """;

        private const string InboxExistsSQL = @"SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{0}' AND TABLE_NAME = '{1}')";

        /// <summary>
        /// Get the DDL statements to create an Inbox in Postgres
        /// </summary>
        /// <param name="inboxTableName">The name you want to use for the table</param>
        /// <param name="binaryMessagePayload">Should the command body be stored as binary.</param>
        /// <param name="jsonMessagePayload">Should the command body be stored using the JSON/JSONB type.</param>
        /// <param name="schemaName">
        /// Optional Postgres schema name. When non-null, the emitted DDL schema-qualifies the
        /// table as <c>"schemaname"."inboxtablename"</c> (lowercase-then-quote via
        /// <see cref="PgIdentifier"/>) so reserved-keyword names parse cleanly while still
        /// resolving to the same physical table that PG's natural case-fold of unquoted
        /// identifiers would have produced. See the outbox builder for the full rationale.
        /// </param>
        /// <returns>The required DDL</returns>
        public static string GetDDL(string inboxTableName, bool binaryMessagePayload = false, bool jsonMessagePayload = false, string? schemaName = null)
        {
            var qualifiedTable = PgIdentifier.QuoteQualified(schemaName, inboxTableName);
            if (binaryMessagePayload)
            {
                return string.Format(jsonMessagePayload ? JsonbOutboxDDL : BinaryOutboxDDL, qualifiedTable);
            }

            return string.Format(jsonMessagePayload ? JsonOutboxDDL : TextOutboxDDL, qualifiedTable);
        }

        /// <summary>
        /// Get the SQL statements required to test for the existence of an Inbox in Postgres
        /// </summary>
        /// <param name="tableSchema">What is the schema under which we should query for the inbox</param>
        /// <param name="inboxTableName">The name that was used for the Inbox table</param>
        /// <returns>The required SQL</returns>
        public static string GetExistsQuery(string tableSchema, string inboxTableName)
        {
            // information_schema.tables stores PG-folded (lowercase) names. Normalize both
            // configured values so mixed-case defaults match the stored folded form.
            return string.Format(
                InboxExistsSQL,
                PgIdentifier.Normalize(tableSchema),
                PgIdentifier.Normalize(inboxTableName));
        }
    }
}
