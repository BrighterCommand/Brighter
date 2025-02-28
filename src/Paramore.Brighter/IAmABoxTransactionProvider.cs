using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// This is a marker interface to indicate that this connection provides access to an ambient transaction
    /// </summary>
    public interface IAmABoxTransactionProvider
    {
        /// <summary>
        /// Close any open connection or transaction
        /// </summary>
        void Close();

        /// <summary>
        /// Commit any transaction that we are managing
        /// </summary>
        void Commit();

        /// <summary>
        /// Allows asynchronous commit of a transaction 
        /// </summary>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Is there a transaction open?
        /// </summary>
        bool HasOpenTransaction { get; }

        /// <summary>
        /// Is there a shared connection? (Do we maintain state or just create anew)
        /// </summary>
        bool IsSharedConnection { get; }

        /// <summary>
        /// Rollback a transaction that we manage
        /// </summary>
        void Rollback();

        /// <summary>
        /// Rollback a transaction that we manage
        /// </summary>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// This is a marker interface to indicate that this connection provides access to an ambient transaction
    /// </summary>
    public interface IAmABoxTransactionProvider<T> : IAmABoxTransactionProvider
    {        
        /// <summary>
        /// Gets an existing transaction; creates a new one from the connection if it does not exist and we support
        /// sharing of connections and transactions. You are responsible for committing the transaction.
        /// </summary>
        /// <returns>A database transaction</returns>
        T GetTransaction();
        
        /// <summary>
        /// Gets an existing transaction; creates a new one from the connection if it does not exist and we support
        /// sharing of connections and transactions. You are responsible for committing the transaction.
        /// </summary>
        /// <returns>A database transaction</returns>
        Task<T> GetTransactionAsync(CancellationToken cancellationToken = default);
    }
    
}
