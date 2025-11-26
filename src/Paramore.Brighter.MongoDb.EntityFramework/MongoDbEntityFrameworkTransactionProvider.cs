#region Licence
/* The MIT License (MIT)
Copyright © 2025 Jakub Syty <jakub.nekro@gmail.com>

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
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Storage;

namespace Paramore.Brighter.MongoDb.EntityFramework
{
    /// <summary>
    /// A transaction provider that uses the same session as EF Core for MongoDB
    /// </summary>
    /// <typeparam name="T">The Db Context to take the session from</typeparam>
    public class MongoDbEntityFrameworkTransactionProvider<T> : IAmARelationalDbConnectionProvider, IAmABoxTransactionProvider<IClientSessionHandle> where T : DbContext
    {
        private readonly T _context;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MongoDbEntityFrameworkTransactionProvider{T}"/> class.
        /// </summary>
        /// <param name="context">The DbContext to use for transactions.</param>
        public MongoDbEntityFrameworkTransactionProvider(T context)
        {
            _context = context;
        }
        /// <summary>
        /// Close the transaction
        /// </summary>
        public void Close()
        {
            _context.Database.CurrentTransaction?.Dispose();
        }

        /// <summary>
        /// Commit the transaction
        /// </summary>
        public void Commit()
        {
            _context.Database.CurrentTransaction?.Commit();
        }

        /// <summary>
        /// Commit the transaction
        /// </summary>
        /// <returns>An awaitable Task</returns>
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            var currentTransaction = _context.Database.CurrentTransaction;
            if (currentTransaction is not null)
            {
                await currentTransaction.CommitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Rollback the transaction
        /// </summary>
        public void Rollback()
        {
            _context.Database.CurrentTransaction?.Rollback();
        }

        /// <summary>
        /// Rollback the transaction
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>An awaitable Task</returns>
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            var currentTransaction = _context.Database.CurrentTransaction;
            if (currentTransaction is not null)
            {
                await currentTransaction.RollbackAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Get the current connection of the database context
        /// </summary>
        /// <returns>The DbConnection that is in use</returns>
        public DbConnection GetConnection()
        {
            return _context.Database.GetDbConnection();
        }

        /// <summary>
        /// Get the current connection of the database context
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>The DbConnection that is in use</returns>
        public Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_context.Database.GetDbConnection());
        }

        /// <summary>
        /// Get the ambient Transaction
        /// </summary>
        /// <returns>The IClientSessionHandle</returns>
        public IClientSessionHandle GetTransaction()
        {
            // If there is no current transaction, we create a new one
            var currentTransaction = _context.Database.CurrentTransaction ?? _context.Database.BeginTransaction();
            if (currentTransaction is not MongoTransaction mongoTransaction)
            {
                throw new InvalidOperationException("The current transaction is not a MongoTransaction");
            }
            // use reflection to access property named Session of type IClientSessionHandle that is in the mongoTransaction
            // this is brittle and I'm currently in a discussion with the Mongo team about making this public
            if (MongoTransactionHelper.SessionProperty.GetValue(mongoTransaction) is not IClientSessionHandle session)
            {
                throw new InvalidOperationException("The current transaction does not have a session");
            }
            return session;
        }

        /// <summary>
        /// Get the ambient Transaction
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>The IClientSessionHandle</returns>
        public Task<IClientSessionHandle> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetTransaction());
        }

        /// <summary>
        /// Gets a value indicating whether there is an open transaction.
        /// </summary>
        public bool HasOpenTransaction => _context.Database.CurrentTransaction is not null;

        /// <summary>
        /// Gets a value indicating whether the connection is shared.
        /// </summary>
        public bool IsSharedConnection => true;
    }
    
    /// <summary>
    /// Helper class for accessing private properties of MongoTransaction.
    /// This is a separate class to avoid a warning about static members in generic types.
    /// </summary>
    file static class MongoTransactionHelper
    {
        /// <summary>
        /// Gets the PropertyInfo for the Session property. We are keeping that static for performance reasons.
        /// </summary>
        public static PropertyInfo SessionProperty { get; } = typeof(MongoTransaction).GetProperty("Session", BindingFlags.Instance | BindingFlags.NonPublic)!;
    }
}
