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
    public class SqliteTransactionProvider : RelationalDbTransactionProvider 
    {
        private readonly string _connectionString;

        /// <summary>
        /// Create a connection provider for Sqlite using a connection string for Db access
        /// </summary>
        /// <param name="configuration">The configuration of the Sqlite database</param>
        public SqliteTransactionProvider(IAmARelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString)); 
            _connectionString = configuration.ConnectionString;
        }
        
        /// <summary>
        /// Commit the transaction
        /// </summary>
        /// <returns>An awaitable Task</returns>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task CommitAsync(CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (HasOpenTransaction)
            {
#if NETSTANDARD2_0        
                ((SqliteTransaction)Transaction!).Commit();
#else
                await ((SqliteTransaction)Transaction!).CommitAsync(cancellationToken);
#endif
                Transaction = null;
            }
            
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// This is a shared connection, so you should use this interface to manage it  
        /// </summary>
        /// <returns>A database connection</returns>
        public override DbConnection GetConnection()
        {
            if (Connection == null) { Connection = new SqliteConnection(_connectionString);}
            if (Connection.State != ConnectionState.Open)
                Connection.Open();
            return Connection;
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// This is a shared connection, so you should use this interface to manage it  
        /// </summary>
        /// <returns>A database connection</returns> 
        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if(Connection == null) { Connection = new SqliteConnection(_connectionString);}

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync(cancellationToken);
            return Connection;
        }

        /// <summary>
        /// Creates and opens a Sqlite Transaction
        /// This is a shared transaction and you should manage it through the unit of work
        /// </summary>
        /// <returns>DbTransaction</returns>
        public override DbTransaction GetTransaction()
        {
            if (Connection == null) Connection = GetConnection();
            if (!HasOpenTransaction)
                Transaction = Connection.BeginTransaction();
            return Transaction!;
        }

        /// <summary>
        /// Creates and opens a Sqlite Transaction
        /// This is a shared transaction and you should manage it through the unit of work
        /// </summary>
        /// <returns>DbTransaction</returns>
         public override async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (Connection == null) Connection = await GetConnectionAsync(cancellationToken);
            if (!HasOpenTransaction)
#if NETSTANDARD2_0
               Transaction = Connection.BeginTransaction();
#else
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Transaction = await Connection.BeginTransactionAsync(cancellationToken);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#endif         
            return Transaction!;
        }
    }
}
