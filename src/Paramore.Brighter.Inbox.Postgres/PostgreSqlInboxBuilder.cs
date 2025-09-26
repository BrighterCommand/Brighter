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

namespace Paramore.Brighter.Inbox.Postgres
{
    /// <summary>
    /// Provide SQL statement helpers for creation of an Inbox
    /// </summary>
    public class PostgreSqlInboxBuilder
    {
        private const string OutboxDDL = @"
                    CREATE TABLE {0}
                        (
                            CommandId VARCHAR(256) NOT NULL ,
                            CommandType VARCHAR(256) NULL ,
                            CommandBody TEXT NULL ,
                            Timestamp timestamptz  NULL ,
                            ContextKey VARCHAR(256) NULL,
                            PRIMARY KEY (CommandId, ContextKey)
                        );";
        
        private const string InboxExistsSQL = @"SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{0}' AND TABLE_NAME = '{1}')";
 
        /// <summary>
        /// Get the DDL statements to create an Inbox in Postgres
        /// </summary>
        /// <param name="inboxTableName">The name you want to use for the table</param>
        /// <returns>The required DDL</returns>
        public static string GetDDL(string inboxTableName)
        {
            return string.Format(OutboxDDL, inboxTableName);
        }

        /// <summary>
        /// Get the SQL statements required to test for the existence of an Inbox in Postgres
        /// </summary>
        /// <param name="tableSchema">What is the schema under which we should query for the inbox</param>
        /// <param name="inboxTableName">The name that was used for the Inbox table</param>
        /// <returns>The required SQL</returns>
        public static string GetExistsQuery(string tableSchema, string inboxTableName)
        {
            return string.Format(InboxExistsSQL, tableSchema, inboxTableName);
        }
    }
}
