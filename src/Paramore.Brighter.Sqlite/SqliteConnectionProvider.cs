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

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.Sqlite
{
    /// <summary>
    /// A connection provider for Sqlite 
    /// </summary>
    public class SqliteConnectionProvider : RelationalDbConnectionProvider, IAmATransactionConnectionProvider
    {
        private readonly string _connectionString;

        /// <summary>
        /// Create a connection provider for Sqlite using a connection string for Db access
        /// </summary>
        /// <param name="configuration">The configuration of the Sqlite database</param>
        public SqliteConnectionProvider(IAmARelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString)); 
            _connectionString = configuration.ConnectionString;
        }
        
        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// </summary>
        /// <returns>A database connection</returns>
        public override DbConnection GetConnection() =>  Connection ?? (Connection = new SqliteConnection(_connectionString));

        public override async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            Connection ??= GetConnection();
            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync(cancellationToken);
            if (!HasOpenTransaction)
                Transaction = await Connection.BeginTransactionAsync(cancellationToken);
            return Transaction;
        }
    }
}
