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
    public class PostgreSqlConfiguration : RelationalDatabaseOutboxConfiguration
    {
        /// <summary>
        /// Initialises a new instance of <see cref="PostgreSqlConfiguration" class/>
        /// </summary>
        /// <param name="connectionString">The connection string to the database</param>
        /// <param name="outBoxTableName">The name of the outbox within the table</param>
        /// <param name="inboxTableName">The name of the inbox table</param>
        /// <param name="queueStoreTable">A store for messages to be written</param>
        /// <param name="binaryMessagePayload">Do we store the payload as text or binary</param>
        public PostgreSqlConfiguration(
            string connectionString,
            string outBoxTableName = null,
            string inboxTableName = null,
            string queueStoreTable = null,
            bool binaryMessagePayload = false) 
            : base(connectionString, outBoxTableName, queueStoreTable, binaryMessagePayload)
        {
            InBoxTableName = inboxTableName;
        }

        /// <summary>
        /// Gets the name of the inbox table.
        /// </summary>
        /// <value>The name of the inbox table.</value>
        public string InBoxTableName { get; private set; }
    }
}
