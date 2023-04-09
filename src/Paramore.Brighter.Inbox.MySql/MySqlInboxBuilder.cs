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
                `CommandId` CHAR(36) NOT NULL , 
                `CommandType` VARCHAR(256) NOT NULL , 
                `CommandBody` TEXT NOT NULL , 
                `Timestamp` TIMESTAMP(4) NOT NULL , 
                `ContextKey` VARCHAR(256)  NULL , 
                PRIMARY KEY (`CommandId`)
            ) ENGINE = InnoDB;";

        const string InboxExistsQuery = @"SHOW TABLES LIKE '{0}'; ";


        /// <summary>
        /// Gets the DDL statements to create an Inbox in MySQL
        /// </summary>
        /// <param name="imboxTableName">The Inbox Table Name</param>
        public static string GetDDL(string imboxTableName)
        {
            return string.Format(OutboxDDL, imboxTableName);
        }

        /// <summary>
        /// Gets the SQL statements required to check for the existence of an Inbox in MySQL
        /// </summary>
        /// <param name="inboxTableName"></param>
        /// <returns>The SQL to test for the existence of an Inbox</returns>
        /// <exception cref="InvalidEnumArgumentException"></exception>
        public static string GetExistsQuery(string inboxTableName)
        {
            if (string.IsNullOrEmpty(inboxTableName))
                throw new InvalidEnumArgumentException($"You must provide a tablename for the inbox table");
            return string.Format(InboxExistsQuery, inboxTableName);
        }
    }
}
