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
        const string TextOutboxDdl = 
            """
            CREATE TABLE {0} ( 
                `MessageId`VARCHAR(255) NOT NULL , 
                `Topic` VARCHAR(255) NOT NULL , 
                `MessageType` VARCHAR(32) NOT NULL , 
                `Timestamp` TIMESTAMP(3) NOT NULL , 
                `CorrelationId`VARCHAR(255) NULL ,
                `ReplyTo` VARCHAR(255) NULL ,
                `ContentType` VARCHAR(128) NULL , 
                `PartitionKey` VARCHAR(128) NULL , 
                `Dispatched` TIMESTAMP(3) NULL , 
                `HeaderBag` TEXT NOT NULL , 
                `Body` TEXT NOT NULL , 
                `Source`  VARCHAR(255) NULL,
                `Type`  VARCHAR(255) NULL,
                `DataSchema`  VARCHAR(255) NULL,
                `Subject`  VARCHAR(255) NULL,
                `TraceParent`  VARCHAR(255) NULL,
                `TraceState`  VARCHAR(255) NULL,
                `Baggage`  TEXT NULL,
                `Created` TIMESTAMP(3) NOT NULL DEFAULT NOW(3),
                `CreatedID` INT(11) NOT NULL AUTO_INCREMENT, 
                UNIQUE(`CreatedID`),
                PRIMARY KEY (`MessageId`)
            ) ENGINE = InnoDB;
            """;
        
        const string BinaryOutboxDdl = 
            """
            CREATE TABLE {0} ( 
                `MessageId` VARCHAR(255) NOT NULL , 
                `Topic` VARCHAR(255) NOT NULL , 
                `MessageType` VARCHAR(32) NOT NULL , 
                `Timestamp` TIMESTAMP(3) NOT NULL , 
                `CorrelationId` VARCHAR(255) NULL ,
                `ReplyTo` VARCHAR(255) NULL ,
                `ContentType` VARCHAR(128) NULL ,  
                `PartitionKey` VARCHAR(128) NULL ,
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
                `Created` TIMESTAMP(3) NOT NULL DEFAULT NOW(3),
                `CreatedID` INT(11) NOT NULL AUTO_INCREMENT,
                UNIQUE(`CreatedID`),
                PRIMARY KEY (`MessageId`)
            ) ENGINE = InnoDB;
            """;

        const string outboxExistsQuery = @"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{0}') AS TableExists;";

        /// <summary>
        /// Get the DDL that describes the table we will store messages in
        /// </summary>
        /// <param name="outboxTableName">The name of the table to store messages in</param>
        /// <param name="hasBinaryMessagePayload">Should the message body be stored as binary? Conversion of binary data to/from UTF-8 is lossy</param>
        /// <returns></returns>
        public static string GetDDL(string outboxTableName, bool hasBinaryMessagePayload = false)
        {
            if (string.IsNullOrEmpty(outboxTableName))
                throw new ArgumentNullException(outboxTableName, $"You must provide a tablename for the OutBox table");

            return string.Format(hasBinaryMessagePayload ? BinaryOutboxDdl : TextOutboxDdl, outboxTableName);
        }

        /// <summary>
        /// Gets the SQL statements required to check for the existence of an Outbox in MySQL
        /// </summary>
        /// <param name="inboxTableName"></param>
        /// <returns></returns>
        /// <exception cref="InvalidEnumArgumentException"></exception>
        public static string GetExistsQuery(string inboxTableName)
        {
            if (string.IsNullOrEmpty(inboxTableName))
                throw new InvalidEnumArgumentException($"You must provide a tablename for the  OutBox table");
            return string.Format(outboxExistsQuery, inboxTableName);
        }
    }
}
