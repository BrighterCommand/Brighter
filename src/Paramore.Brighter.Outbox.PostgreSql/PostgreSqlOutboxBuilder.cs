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
    public class PostgreSqlOutboxBulder
    {
        const string OutboxDdl = @"
       CREATE TABLE {0}
            (
                Id BIGSERIAL PRIMARY KEY,
                MessageId UUID UNIQUE NOT NULL,
                Topic VARCHAR(255) NULL,
                MessageType VARCHAR(32) NULL,
                Timestamp timestamptz NULL,
                CorrelationId uuid NULL,
                ReplyTo VARCHAR(255) NULL,
                ContentType VARCHAR(128) NULL,  
                Dispatched timestamptz NULL,
                HeaderBag TEXT NULL,
                Body TEXT NULL
            );
        ";
        
        private const string OutboxExistsSQL = @"SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'{0}'";
        
        /// <summary>
        /// Get the DDL required to create the Outbox in Postgres
        /// </summary>
        /// <param name="outboxTableName">The name you want to use for the table</param>
        /// <returns>The required DDL</returns>
        public static string GetDDL(string outboxTableName)
        {
            return string.Format(OutboxDdl, outboxTableName);
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
