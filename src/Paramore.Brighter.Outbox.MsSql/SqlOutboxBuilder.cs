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

using System;

namespace Paramore.Brighter.Outbox.MsSql
{
    /// <summary>
    /// Provide SQL statement helpers for creation of an Outbox
    /// </summary>
    public class SqlOutboxBuilder
    {
        const string TextOutboxDdl = @"
        CREATE TABLE {0}
            (
              [Id] [BIGINT] NOT NULL IDENTITY ,
              [MessageId] NVARCHAR(255) NOT NULL,
              [Topic] NVARCHAR(255) NULL,
              [MessageType] NVARCHAR(32) NULL,
              [Timestamp] DATETIME NULL,
              [CorrelationId] NVARCHAR(255) NULL,
              [ReplyTo] NVARCHAR(255) NULL,
              [ContentType] NVARCHAR(128) NULL,  
              [PartitionKey] NVARCHAR(255) NULL, 
              [WorkflowId] NVARCHAR(255) NULL,
              [JobId] NVARCHAR(255) NULL,
              [Dispatched] DATETIME NULL,
              [HeaderBag] NVARCHAR(MAX) NULL,
              [Body] NVARCHAR(MAX) NULL,
              [Source] NVARCHAR(255) NULL,
              [Type] NVARCHAR(255) NULL,
              [DataSchema] NVARCHAR(255) NULL,
              [Subject] NVARCHAR(255) NULL,
              [TraceParent] NVARCHAR(255) NULL,
              [TraceState] NVARCHAR(255) NULL,
              [Baggage] NVARCHAR(MAX) NULL,
              [DataRef] NVARCHAR(255) NULL,
              [SpecVersion] NVARCHAR(255) NULL
              PRIMARY KEY ( [Id] )
            );
        ";
        
        const string BinaryOutboxDdl = @"
        CREATE TABLE {0}
            (
              [Id] [BIGINT] NOT NULL IDENTITY,
              [MessageId] NVARCHAR(255) NOT NULL,
              [Topic] NVARCHAR(255) NULL,
              [MessageType] NVARCHAR(32) NULL,
              [Timestamp] DATETIME NULL,
              [CorrelationId] NVARCHAR(255) NULL,
              [ReplyTo] NVARCHAR(255) NULL,
              [ContentType] NVARCHAR(128) NULL,  
              [PartitionKey] NVARCHAR(255) NULL,
              [WorkflowId] NVARCHAR(255) NULL,
              [JobId] NVARCHAR(255) NULL,
              [Dispatched] DATETIME NULL,
              [HeaderBag] NVARCHAR(MAX) NULL,
              [Body] VARBINARY(MAX) NULL,
              [Source] NVARCHAR(255) NULL,
              [Type] NVARCHAR(255) NULL,
              [DataSchema] NVARCHAR(255) NULL,
              [Subject] NVARCHAR(255) NULL,
              [TraceParent] NVARCHAR(255) NULL,
              [TraceState] NVARCHAR(255) NULL,
              [Baggage] NVARCHAR(MAX) NULL,
              [DataRef] NVARCHAR(255) NULL,
              [SpecVersion] NVARCHAR(255) NULL
              PRIMARY KEY ( [Id] )
            );
        ";
 
        
        private const string OUTBOX_EXISTS_SQL = @"
        IF EXISTS (
            SELECT 1
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.name = '{0}' AND s.name = '{1}'
        )
            SELECT 1 AS TableExists;
        ELSE
            SELECT 0 AS TableExists;";
        /// <summary>
        /// Gets the DDL statements required to create an Outbox in MSSQL
        /// </summary>
        /// <param name="outboxTableName">The name of the Outbox table</param>
        /// <param name="hasBinaryMessagePayload">Should the message body be stored as binary? Conversion of binary data to/from UTF-8 is lossy</param>
        /// <returns>The required DDL</returns>
        public static string GetDDL(string outboxTableName, bool hasBinaryMessagePayload = false)
        {
            if (string.IsNullOrEmpty(outboxTableName))
                throw new ArgumentNullException(outboxTableName, $"You must provide a tablename for the OutBox table");

            return string.Format(hasBinaryMessagePayload ? BinaryOutboxDdl : TextOutboxDdl, outboxTableName);
        }
        
        /// <summary>
        /// Get the SQL statements required to test for the existence of an Inbox in MSSQL, checking both schema and table name.
        /// </summary>
        /// <param name="inboxTableName">The name that was used for the Inbox table.</param>
        /// <param name="schemaName">The schema that was used for the Inbox table.</param>
        /// <returns>The required SQL.</returns>
        public static string GetExistsQuery(string inboxTableName, string schemaName)
        {
            return string.Format(OUTBOX_EXISTS_SQL, inboxTableName, schemaName);
        }
    }
}
