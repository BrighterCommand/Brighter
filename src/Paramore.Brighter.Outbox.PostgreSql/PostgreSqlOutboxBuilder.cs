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
        const string TextOutboxDdl = @"
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
                Body text NULL,
                Source character varying (255) NULL,
                Type character varying (255) NULL,
                DataSchema character varying (255) NULL,
                Subject character varying (255) NULL,
                TraceParent character varying (255) NULL,
                TraceState character varying (255) NULL,
                Baggage text NULL
            );
        ";
        
        const string BinaryOutboxDdl = @"
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
                Baggage text NULL
            );
        ";
        
        private const string OutboxExistsSQL = @"SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{0}')";

        /// <summary>
        /// Get the DDL required to create the Outbox in Postgres
        /// </summary>
        /// <param name="outboxTableName">The name you want to use for the table</param>
        /// <param name="binaryMessagePayload"></param>
        /// <returns>The required DDL</returns>
        public static string GetDDL(string outboxTableName, bool binaryMessagePayload = false)
        {
            return binaryMessagePayload ? string.Format(BinaryOutboxDdl, outboxTableName) : string.Format(TextOutboxDdl, outboxTableName);
        }
        
        /// <summary>
        /// Get the SQL statements required to test for the existence of an Inbox in Postgres
        /// </summary>
        /// <param name="inboxTableName">The name that was used for the Inbox table</param>
        /// <returns>The required SQL</returns>
        public static string GetExistsQuery(string inboxTableName)
        {
            return string.Format(OutboxExistsSQL, inboxTableName);
        }
    }
}
