#region Licence
 /* The MIT License (MIT)
 Copyright © 2021 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
 
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

namespace Paramore.Brighter.Sqlite
{
    public class SqliteConfiguration : RelationalDatabaseOutboxConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteConfiguration"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="outBoxTableName">Name of the outbox table.</param>
        /// <param name="inboxTableName">Name of the inbox table.</param>
        /// <param name="queueStoreTable">Name of the queue store table.</param>
        /// <param name="binaryMessagePayload">Is the message payload binary, or a UTF-8 string, default is false or UTF-8</param>
        public SqliteConfiguration(
            string connectionString,
            string outBoxTableName = null,
            string inboxTableName = null,
            string queueStoreTable = null,
            bool binaryMessagePayload = false)
            : base(connectionString, outBoxTableName, queueStoreTable, binaryMessagePayload)
        {
        }


        /// <summary>
        /// Gets the name of the inbox table.
        /// </summary>
        /// <value>The name of the inbox table.</value>
        public string InBoxTableName { get; private set; }
    }
}
