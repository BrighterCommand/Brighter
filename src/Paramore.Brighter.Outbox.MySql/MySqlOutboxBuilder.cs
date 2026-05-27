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
using System.ComponentModel;

namespace Paramore.Brighter.Outbox.MySql
{
    /// <summary>
    /// Provide SQL statement helpers for creation of an Outbox
    /// </summary>
    public class MySqlOutboxBuilder
    {
        private const string TextOutboxDdl =
            """
            CREATE TABLE IF NOT EXISTS {0} (
                `MessageId`VARCHAR(255) NOT NULL,
                `Topic` VARCHAR(255) NOT NULL,
                `MessageType` VARCHAR(32) NOT NULL,
                `Timestamp` TIMESTAMP(3) NOT NULL,
                `CorrelationId`VARCHAR(255) NULL,
                `ReplyTo` VARCHAR(255) NULL,
                `ContentType` VARCHAR(128) NULL,
                `PartitionKey` VARCHAR(128) NULL,
                `WorkflowId` VARCHAR(255) NULL,
                `JobId` VARCHAR(255) NULL,
                `Dispatched` TIMESTAMP(3) NULL,
                `HeaderBag` TEXT NOT NULL,
                `Body` TEXT NOT NULL,
                `Source`  VARCHAR(255) NULL,
                `Type`  VARCHAR(255) NULL,
                `DataSchema`  VARCHAR(255) NULL,
                `Subject`  VARCHAR(255) NULL,
                `TraceParent`  VARCHAR(255) NULL,
                `TraceState`  VARCHAR(255) NULL,
                `Baggage`  TEXT NULL,
                `DataRef` VARCHAR(255) NULL,
                `SpecVersion` VARCHAR(255) NULL,
                `Created` TIMESTAMP(3) NOT NULL DEFAULT NOW(3),
                `CreatedID` INT(11) NOT NULL AUTO_INCREMENT,
                UNIQUE(`CreatedID`),
                PRIMARY KEY (`MessageId`)
            ) ENGINE = InnoDB;
            """;

        private const string BinaryOutboxDdl =
            """
            CREATE TABLE IF NOT EXISTS {0} ( 
                `MessageId` VARCHAR(255) NOT NULL , 
                `Topic` VARCHAR(255) NOT NULL , 
                `MessageType` VARCHAR(32) NOT NULL , 
                `Timestamp` TIMESTAMP(3) NOT NULL , 
                `CorrelationId` VARCHAR(255) NULL ,
                `ReplyTo` VARCHAR(255) NULL ,
                `ContentType` VARCHAR(128) NULL ,  
                `PartitionKey` VARCHAR(128) NULL ,
                `WorkflowId` VARCHAR(255) NULL ,
                `JobId` VARCHAR(255) NULL ,
                `Dispatched` TIMESTAMP(3) NULL , 
                `HeaderBag` TEXT NOT NULL , 
                `Body` BLOB NOT NULL , 
                `Source`  VARCHAR(255) NULL,
                `Type`  VARCHAR(255) NULL,
                `DataSchema`  VARCHAR(255) NULL,
                `Subject`  VARCHAR(255) NULL,
                `TraceParent`  VARCHAR(255) NULL,
                `TraceState`  VARCHAR(255) NULL,
                `Baggage`  TEXT NULL,
                `DataRef` VARCHAR(255) NULL,
                `SpecVersion` VARCHAR(255) NULL,
                `Created` TIMESTAMP(3) NOT NULL DEFAULT NOW(3),
                `CreatedID` INT(11) NOT NULL AUTO_INCREMENT,
                UNIQUE(`CreatedID`),
                PRIMARY KEY (`MessageId`)
            ) ENGINE = InnoDB;
            """;

        private const string OutboxExistsQuery = @"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = '{0}' AND table_name = '{1}') AS TableExists;";

        /// <summary>
        /// Get the DDL that describes the table we will store messages in
        /// </summary>
        /// <param name="outboxTableName">The name of the table to store messages in</param>
        /// <param name="hasBinaryMessagePayload">Should the message body be stored as binary? Conversion of binary data to/from UTF-8 is lossy</param>
        /// <param name="schemaName">
        /// Optional MySQL schema (database) name. When non-null, the emitted DDL schema-qualifies
        /// the table as <c>`schemaName`.`outboxTableName`</c>; otherwise the table is emitted
        /// unqualified and lands in the connection's bound database (<c>DATABASE()</c>). In MySQL
        /// "schema" is synonymous with "database"; per PR #4039 reviewer item M4-1 (F1c),
        /// configuring <see cref="Paramore.Brighter.IAmARelationalDatabaseConfiguration.SchemaName"/>
        /// to a database different from the connection's bound database requires schema-qualified
        /// DDL — without it, detection looks in SchemaName while creation lands in DATABASE().
        /// </param>
        /// <returns></returns>
        public static string GetDDL(string outboxTableName, bool hasBinaryMessagePayload = false, string? schemaName = null)
        {
            if (string.IsNullOrEmpty(outboxTableName))
                throw new ArgumentNullException(outboxTableName, $"You must provide a tablename for the OutBox table");

            var qualifiedTable = schemaName is null
                ? outboxTableName
                : $"`{schemaName}`.`{outboxTableName}`";
            return string.Format(hasBinaryMessagePayload ? BinaryOutboxDdl : TextOutboxDdl, qualifiedTable);
        }

        /// <summary>
        /// Gets the SQL statements required to check for the existence of an Outbox in MySQL
        /// </summary>
        /// <param name="tableSchema">What schema should we check for an outbox in</param>
        /// <param name="outboxTableName">What is the name of the outbox table that we want to check for?</param>
        /// <returns></returns>
        /// <exception cref="InvalidEnumArgumentException"></exception>
        public static string GetExistsQuery(string tableSchema, string outboxTableName)
        {
            if (string.IsNullOrEmpty(tableSchema) || string.IsNullOrEmpty(outboxTableName))
                throw new InvalidEnumArgumentException($"You must provide a tablename for the  OutBox table");
            return string.Format(OutboxExistsQuery, tableSchema, outboxTableName);
        }
    }
}
