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

namespace Paramore.Brighter.Inbox.MsSql
{
    /// <summary>
    /// Provide SQL statement helpers for creation of an Inbox
    /// </summary>
    public class SqlInboxBuilder
    {
        private const string InboxDDL = @"
                    CREATE TABLE {0}
                        (
                            [Id] [BIGINT] IDENTITY(1, 1) NOT NULL ,
                            [CommandId] [NVARCHAR](256) NOT NULL ,
                            [CommandType] [NVARCHAR](256) NULL ,
                            [CommandBody] [NVARCHAR](MAX) NULL ,
                            [Timestamp] [DATETIME] NULL ,
                            [ContextKey] [NVARCHAR](256) NULL,
                            PRIMARY KEY ( [Id] )
                        );";

        private const string INBOX_EXISTS_SQL = @"IF EXISTS (
            SELECT 1
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.name = '{0}' AND s.name = '{1}'
        ) SELECT 1 AS TableExists; ELSE SELECT 0 AS TableExists;";

        /// <summary>
        /// Get the DDL statements to create an Inbox in MSSQL
        /// </summary>
        /// <param name="inboxTableName">The name you want to use for the table</param>
        /// <returns>The required DDL</returns>
         public static string GetDDL(string inboxTableName)
        {
            return string.Format(InboxDDL, inboxTableName);
        }
        
        /// <summary>
        /// Get the SQL statements required to test for the existence of an Inbox in MSSQL.
        /// </summary>
        /// <param name="inboxTableName">The name that was used for the Inbox table.</param>
        /// <param name="schemaName">The schema name for the Inbox table. Defaults to 'dbo'.</param>
        /// <returns>The required SQL.</returns>
        public static string GetExistsQuery(string inboxTableName, string schemaName = "dbo") =>
            string.Format(INBOX_EXISTS_SQL, inboxTableName, schemaName);

    }
}
