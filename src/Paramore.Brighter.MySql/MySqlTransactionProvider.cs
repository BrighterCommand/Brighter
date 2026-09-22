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
using MySqlConnector;

namespace Paramore.Brighter.MySql
{
    /// <summary>
    /// A connection provider that uses the connection string to create a connection
    /// </summary>
    public class MySqlTransactionProvider : RelationalDbTransactionProvider 
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initialise a new instance of MySql Connection provider from a connection string
        /// </summary>
        /// <param name="configuration">MySql Configuration</param>
        public MySqlTransactionProvider(IAmARelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));
            _connectionString = configuration!.ConnectionString;
        }
        
        /// <summary>
        /// Commit the transaction
        /// </summary>
        /// <returns>An awaitable Task</returns>
        public override Task CommitAsync(CancellationToken cancellationToken)
        {
            if (HasOpenTransaction)
            {
                ((MySqlTransaction)Transaction!).CommitAsync(cancellationToken);
                Transaction = null;
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates and opens a MySql Connection
        /// </summary>
        /// <returns></returns>
        public override DbConnection GetConnection()
        {
            if (Connection == null) Connection = new MySqlConnection(_connectionString);
            if (Connection.State != ConnectionState.Open) Connection.Open();
            return Connection;
        }

        /// <summary>
        /// Creates and opens a MySql Connection
        /// This is a shared connection and you should manage it through the unit of work
        /// </summary>
        /// <returns></returns>
        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (Connection == null) Connection = new MySqlConnection(_connectionString);
            if (Connection.State != ConnectionState.Open) await Connection.OpenAsync();
            return Connection;
        }
        
        /// <summary>
        /// Creates and opens a MySql Transaction
        /// This is a shared transaction and you should manage it through the unit of work
        /// </summary>
        /// <returns>A shared transaction</returns>
        public override DbTransaction GetTransaction()
        {
            if (Connection == null) Connection = GetConnection();
            if (!HasOpenTransaction)
                Transaction = ((MySqlConnection) Connection).BeginTransaction();
            return Transaction!;
        }

        /// <summary>
        /// Creates and opens a MySql Transaction
        /// This is a shared transaction and you should manage it through the unit of work
        /// </summary>
        /// <returns>A shared transaction</returns>
        public override async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (Connection == null) Connection = await GetConnectionAsync(cancellationToken);
            if (!HasOpenTransaction)
                Transaction = await ((MySqlConnection) Connection).BeginTransactionAsync(cancellationToken);
            return Transaction!;
        }
 
    }
}
