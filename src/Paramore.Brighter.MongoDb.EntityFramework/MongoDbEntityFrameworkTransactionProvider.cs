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
        private readonly T context;
        
        public MongoDbEntityFrameworkTransactionProvider(T context)
        {
            this.context = context;
        }
        public void Close()
        {
            context.Database.CurrentTransaction?.Dispose();
        }

        /// <summary>
        /// Commit the transaction
        /// </summary>
        public void Commit()
        {
            context.Database.CurrentTransaction?.Commit();
        }

        /// <summary>
        /// Commit the transaction
        /// </summary>
        /// <returns>An awaitable Task</returns>
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            var currentTransaction = context.Database.CurrentTransaction;
            if (currentTransaction is not null)
            {
                await currentTransaction.CommitAsync(cancellationToken);
            }
        }

        public void Rollback()
        {
            context.Database.CurrentTransaction?.Rollback();
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            var currentTransaction = context.Database.CurrentTransaction;
            if (currentTransaction is not null)
            {
                await currentTransaction.RollbackAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Get the current connection of the database context
        /// </summary>
        /// <returns>The NpgsqlConnection that is in use</returns>
        public DbConnection GetConnection()
        {
            return context.Database.GetDbConnection();
        }

        /// <summary>
        /// Get the current connection of the database context
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns></returns>
        public Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(context.Database.GetDbConnection());
        }

        /// <summary>
        /// Get the ambient Transaction
        /// </summary>
        /// <returns>The NpgsqlTransaction</returns>
        public IClientSessionHandle GetTransaction()
        {
            // If there is no current transaction, we create a new one
            var currentTransaction = context.Database.CurrentTransaction ?? context.Database.BeginTransaction();
            if (currentTransaction is not MongoTransaction mongoTransaction)
            {
                throw new InvalidOperationException("The current transaction is not a MongoTransaction");
            }
            // use reflection to access property named Session of type IClientSessionHandle that is in the mongoTransaction
            if (MongoTransactionHelper.SessionProperty.GetValue(mongoTransaction) is not IClientSessionHandle session)
            {
                throw new InvalidOperationException("The current transaction does not have a session");
            }
            return session;
        }

        public Task<IClientSessionHandle> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetTransaction());
        }

        public bool HasOpenTransaction => context.Database.CurrentTransaction is not null;

        public bool IsSharedConnection => true;
    }
    
    file static class MongoTransactionHelper
    {
        public static PropertyInfo SessionProperty { get; } = typeof(MongoTransaction).GetProperty("Session", BindingFlags.Instance | BindingFlags.NonPublic)!;
    }
}
