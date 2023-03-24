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

namespace Paramore.Brighter.Outbox.Sqlite
{
    /// <summary>
    /// Provide SQL statement helpers for creation of an Outbox
    /// Note that due to a case-related bug in Microsoft.Data.Core when comparing GUIDs in Sqlite, we use COLLATE NOCASE for the MessageId
    /// </summary>
    public class SqliteOutboxBuilder
    {
        const string TextOutboxDdl = @"CREATE TABLE {0} 
                                    (
                                        [MessageId] TEXT NOT NULL COLLATE NOCASE,
                                        [Topic] TEXT NULL,
                                        [MessageType] TEXT NULL,
                                        [Timestamp] TEXT NULL,
                                        [CorrelationId] TEXT NULL,
                                        [ReplyTo] TEXT NULL,
                                        [ContentType] TEXT NULL,  
                                        [Dispatched] TEXT NULL,
                                        [HeaderBag] TEXT NULL,
                                        [Body] TEXT NULL,
                                        CONSTRAINT[PK_MessageId] PRIMARY KEY([MessageId])
                                    );";
        
        const string BinaryOutboxDdl = @"CREATE TABLE {0} 
                                    (
<<<<<<< HEAD
                                        [MessageId] uniqueidentifier NOT NULL,
                                        [Topic] nvarchar(255) NULL,
                                        [MessageType] nvarchar(32) NULL,
                                        [Timestamp] datetime NULL,
                                        [CorrelationId] UNIQUEIDENTIFIER NULL,
                                        [ReplyTo] NVARCHAR(255) NULL,
                                        [ContentType] NVARCHAR(128) NULL,  
                                        [Dispatched] datetime NULL,
                                        [HeaderBag] ntext NULL,
                                        [Body] binary NULL,
=======
                                        [MessageId] TEXT NOT NULL COLLATE NOCASE,
                                        [Topic] TEXT NULL,
                                        [MessageType] TEXT NULL,
                                        [Timestamp] TEXT NULL,
                                        [CorrelationId] TEXT NULL,
                                        [ReplyTo] TEXT NULL,
                                        [ContentType] TEXT NULL,  
                                        [Dispatched] TEXT NULL,
                                        [HeaderBag] TEXT NULL,
                                        [Body] TEXT NULL,
>>>>>>> master
                                        CONSTRAINT[PK_MessageId] PRIMARY KEY([MessageId])
                                    );";
         

        private const string OutboxExistsQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='{0}';";

        /// <summary>
        /// Get the DDL statements to create an Outbox in Sqlite
        /// </summary>
        /// <param name="outboxTableName"></param>
        /// <param name="hasBinaryMessagePayload">Is the payload for the message binary or UTF-8. Defaults to false, or UTF-8</param>
        /// <returns>The required DDL</returns>
        public static string GetDDL(string outboxTableName, bool hasBinaryMessagePayload = false)
        {
            return hasBinaryMessagePayload ? string.Format(BinaryOutboxDdl, outboxTableName) : string.Format(TextOutboxDdl, outboxTableName);
        }
        
        /// <summary>
        /// Get the SQL statements required to test for the existence of an Outbox in Sqlite
        /// </summary>
        /// <param name="outboxTableName">The name that was used for the Outbox table</param>
        /// <returns>The required SQL</returns>
        public static string GetExistsQuery(string outboxTableName)
        {
            return string.Format(OutboxExistsQuery, outboxTableName);
        }
    }
}
