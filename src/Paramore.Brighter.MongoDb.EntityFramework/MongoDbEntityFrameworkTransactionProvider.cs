using System;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
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
    public class MongoDbEntityFrameworkTransactionProvider<T> : IAmARelationalDbConnectionProvider, IAmABoxTransactionProvider<IClientSessionHandle>, IDisposable where T : DbContext
    {
        private readonly T _context;
    
        private bool _disposed;
    
        private DbConnection? _connection;
    
        private DbTransaction? _transaction;

        /// <summary>
        /// A transaction provider that uses the same session as EF Core for MongoDB
        /// </summary>
        /// <typeparam name="T">The Db Context to take the session from</typeparam>
        public MongoDbEntityFrameworkTransactionProvider(T context)
        {
            _context = context;
        }

        ~MongoDbEntityFrameworkTransactionProvider() => Dispose(false);
    
        public void Close()
        {
            if (!HasOpenTransaction)
            {
                _transaction?.Dispose();
                _transaction = null;
            }
    
            if (!IsSharedConnection)
            {
                _connection?.Close();
            }
        }
    
        /// <summary>
        /// Commit the transaction
        /// </summary>
        public void Commit()
        {
            if (HasOpenTransaction)
            {
                _context.Database.CurrentTransaction?.Commit();
            }
        }
    
        /// <summary>
        /// Commit the transaction
        /// </summary>
        /// <returns>An awaitable Task</returns>
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            if (HasOpenTransaction)
            {
                _context.Database.CurrentTransaction?.CommitAsync(cancellationToken);
            }
    
            return Task.CompletedTask;
        }
    
        public void Rollback()
        {
            if (!HasOpenTransaction)
            {
                return;
            }
    
            try { _transaction!.Rollback(); } catch (Exception) { /*ignore*/ }
            _transaction = null;
        }
    
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (!HasOpenTransaction)
            {
                return Task.CompletedTask;
            }
    
            try { _transaction!.Rollback(); } catch (Exception) { /*ignore*/ }
            _transaction = null;
    
            return Task.CompletedTask;
        }
    
        /// <summary>
        /// Get the current connection of the database context
        /// </summary>
        /// <returns>The NpgsqlConnection that is in use</returns>
        public DbConnection GetConnection()
        {
            return _context.Database.GetDbConnection();
        }
    
        /// <summary>
        /// Get the current connection of the database context
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns></returns>
        public Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<DbConnection>();
            tcs.SetResult(_context.Database.GetDbConnection());
            return tcs.Task;
        }
    
        /// <summary>
        /// Get the ambient Transaction
        /// </summary>
        /// <returns>The NpgsqlTransaction</returns>
        public IClientSessionHandle GetTransaction()
        {
            // If there is no current transaction, we create a new one
            var currentTransaction = _context.Database.CurrentTransaction ?? _context.Database.BeginTransaction();
            if (currentTransaction is not MongoTransaction mongoTransaction)
            {
                throw new InvalidOperationException("The current transaction is not a MongoTransaction");
            }
            // use reflection to access property named Session of type IClientSessionHandle that is in the mongoTransaction
            // it is internal, so we need to use reflection to access it
            // ideally we would use just DbTransaction directly, but it's currently impossible due to compatibility with the base mongodb outbox provider
            if (MongoTransactionHelper.SessionProperty.GetValue(mongoTransaction) is not IClientSessionHandle session)
            {
                throw new InvalidOperationException("The current transaction does not have a session");
            }
            return session;
        }
    
        public Task<IClientSessionHandle> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<IClientSessionHandle>();
    
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled(CancellationToken.None);
            }
    
            tcs.SetResult(GetTransaction());
            return tcs.Task;
        }
    
        [MemberNotNullWhen(true, nameof(_transaction))]
        public bool HasOpenTransaction => _transaction is not null;
    
        public bool IsSharedConnection => true;
    
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    
        protected void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
    
            if (disposing)
            {
                _connection?.Dispose();
                _transaction?.Dispose();
            }
            _connection = null;
            _transaction = null;
            _disposed = true;
        }
    }
    
    static file class MongoTransactionHelper
    {
        public static PropertyInfo SessionProperty { get; } = typeof(MongoTransaction).GetProperty("Session", BindingFlags.Instance | BindingFlags.NonPublic)!;
    }
}
