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

namespace Paramore.Brighter.Inbox.Sqlite
{
    /// <summary>
    /// Provide SQL statement helpers for creation of an Inbox
    /// </summary>
    public class SqliteInboxBuilder
    {
        private const string TextInboxDDL = """
            CREATE TABLE IF NOT EXISTS {0} 
            (
                [CommandId] UNIQUEIDENTIFIER CONSTRAINT PK_MessageId PRIMARY KEY,
                [CommandType] NVARCHAR(256),
                [CommandBody] NTEXT,
                [Timestamp] TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                [ContextKey] NVARCHAR(256)
            );
            """;

        private const string BinaryInboxDDL =
            """
            CREATE TABLE IF NOT EXISTS {0}
            (
                [CommandId] UNIQUEIDENTIFIER CONSTRAINT PK_MessageId PRIMARY KEY,
                [CommandType] NVARCHAR(256),
                [CommandBody] BLOB NULL,
                [Timestamp] TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                [ContextKey] NVARCHAR(256)
            );
            """;


        private const string InboxExistsSQL = "SELECT name FROM sqlite_master WHERE type='table' AND name='{0}'";

        /// <summary>
        /// Get the DDL statements to create an Inbox in Sqlite
        /// </summary>
        /// <param name="inboxTableName">The name you want to use for the table</param>
        /// <returns>The required DDL</returns>
        public static string GetDDL(string inboxTableName, bool binary = false)
        {
            if (binary)
            {
                return string.Format(BinaryInboxDDL, inboxTableName);
            }

            return string.Format(TextInboxDDL, inboxTableName);
        }

        /// <summary>
        /// Get the SQL statements required to test for the existence of an Inbox in Sqlite
        /// </summary>
        /// <param name="inboxTableName">The name that was used for the Inbox table</param>
        /// <returns>The required SQL</returns>
        public static string GetExistsQuery(string inboxTableName)
        {
            return string.Format(InboxExistsSQL, inboxTableName);
        }
    }
}
