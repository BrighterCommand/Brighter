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

using System.ComponentModel;

namespace Paramore.Brighter.Inbox.MySql
{
    /// <summary>
    /// Provide SQL statement helpers for creation of an Inbox
    /// </summary>
    public class MySqlInboxBuilder
    {
        private const string OutboxDDL = @"CREATE TABLE {0} 
            ( 
                `CommandId` VARCHAR(255) NOT NULL , 
                `CommandType` VARCHAR(256) NOT NULL , 
                `CommandBody` TEXT NOT NULL , 
                `Timestamp` TIMESTAMP(4) NOT NULL , 
                `ContextKey` VARCHAR(256)  NULL , 
                PRIMARY KEY (`CommandId`)
            ) ENGINE = InnoDB;";

        const string InboxExistsQuery = @"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = '{0}' AND table_name = '{1}') AS TableExists;";

        /// <summary>
        /// Gets the DDL statements to create an Inbox in MySQL
        /// </summary>
        /// <param name="inboxTableName">The Inbox Table Name</param>
        /// <returns></returns>
        public static string GetDDL(string inboxTableName)
        {
            return string.Format(OutboxDDL, inboxTableName);
        }

        /// <summary>
        /// Gets the SQL statements required to check for the existence of an Inbox in MySQL
        /// </summary>
        /// <param name="tableSchema">What is the schema we should check for the table</param>
        /// <param name="inboxTableName">What isthe name for the Inbox in the schema</param>
        /// <returns>The SQL to test for the existence of an Inbox</returns>
        /// <exception cref="InvalidEnumArgumentException"></exception>
        public static string GetExistsQuery(string tableSchema, string inboxTableName)
        {
            if (string.IsNullOrEmpty(tableSchema) || string.IsNullOrEmpty(inboxTableName))
                throw new InvalidEnumArgumentException($"You must provide a table schema and tablename for the inbox table");
            return string.Format(InboxExistsQuery, tableSchema, inboxTableName);
        }
    }
}
