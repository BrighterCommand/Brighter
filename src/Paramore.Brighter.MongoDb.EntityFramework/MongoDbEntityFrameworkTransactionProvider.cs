using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MongoDB.Driver;

namespace Paramore.Brighter.MongoDb.EntityFramework
{
    /// <summary>
    /// A transaction provider that uses the same session as EF Core for MongoDB
    /// </summary>
    /// <typeparam name="T">The Db Context to take the session from</typeparam>
    public class MongoDbEntityFrameworkTransactionProvider<T> : IAmAMongoDbTransactionProvider where T : DbContext
    {
        private readonly T _context;
        private IClientSessionHandle? _session;

        /// <summary>
        /// Constructs an instance from a database context
        /// </summary>
        /// <param name="context">The database context to use</param>
        public MongoDbEntityFrameworkTransactionProvider(T context)
        {
            _context = context;
        }

        /// <summary>
        /// Get the MongoDB client from the database context
        /// </summary>
        public IMongoClient Client
        {
            get
            {
                // Get the MongoDB client from the DbContext
                var database = _context.Database.GetService<IMongoDatabase>();
                return database.Client;
            }
        }

        /// <summary>
        /// Close the session if one is open
        /// </summary>
        public void Close()
        {
            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }
        }

        /// <summary>
        /// Commit the transaction
        /// </summary>
        public void Commit()
        {
            if (HasOpenTransaction)
            {
                _session?.CommitTransaction();
                _session?.Dispose();
                _session = null;
            }
        }

        /// <summary>
        /// Commit the transaction asynchronously
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>An awaitable Task</returns>
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (HasOpenTransaction && _session != null)
            {
                await _session.CommitTransactionAsync(cancellationToken);
                _session.Dispose();
                _session = null;
            }
        }

        /// <summary>
        /// Get the ambient session handle
        /// </summary>
        /// <returns>The IClientSessionHandle</returns>
        public IClientSessionHandle GetTransaction()
        {
            if (_session == null)
            {
                // Try to get existing session from EF Core context, or create a new one
                var database = _context.Database.GetService<IMongoDatabase>();
                _session = database.Client.StartSession();
                _session.StartTransaction();
            }
            return _session;
        }

        /// <summary>
        /// Get the ambient session handle asynchronously
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>The IClientSessionHandle</returns>
        public async Task<IClientSessionHandle> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_session == null)
            {
                // Try to get existing session from EF Core context, or create a new one
                var database = _context.Database.GetService<IMongoDatabase>();
                _session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
                _session.StartTransaction();
            }
            return _session;
        }

        /// <summary>
        /// Get the transaction as IClientSession (for outbox/inbox compatibility)
        /// </summary>
        /// <returns>The IClientSession</returns>
        IClientSession IAmABoxTransactionProvider<IClientSession>.GetTransaction()
        {
            return GetTransaction();
        }

        /// <summary>
        /// Get the transaction asynchronously as IClientSession (for outbox/inbox compatibility)
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>The IClientSession</returns>
        async Task<IClientSession> IAmABoxTransactionProvider<IClientSession>.GetTransactionAsync(CancellationToken cancellationToken)
        {
            return await GetTransactionAsync(cancellationToken);
        }

        /// <summary>
        /// Is there a transaction open?
        /// </summary>
        public bool HasOpenTransaction => _session != null;

        /// <summary>
        /// Is there a shared connection? (Always true for EF Core integration)
        /// </summary>
        public bool IsSharedConnection => true;

        /// <summary>
        /// Rollback the transaction
        /// </summary>
        public void Rollback()
        {
            if (HasOpenTransaction && _session != null)
            {
                try
                {
                    _session.AbortTransaction();
                }
                catch
                {
                    // Ignore
                }

                _session.Dispose();
                _session = null;
            }
        }

        /// <summary>
        /// Rollback the transaction asynchronously
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>An awaitable Task</returns>
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (HasOpenTransaction && _session != null)
            {
                try
                {
                    await _session.AbortTransactionAsync(cancellationToken);
                }
                catch
                {
                    // Ignore
                }

                _session.Dispose();
                _session = null;
            }
        }
    }
}
