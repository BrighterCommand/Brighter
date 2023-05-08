using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Providers an abstraction over a relational database connection; implementations may allow connection and
    /// transaction sharing by holding state, thus a Close method is provided.
    /// </summary>
    public interface IAmARelationalDbConnectionProvider
    {
        /// <summary>
        /// Close any open connection or transaction
        /// </summary>
        void Close();
        
        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// </summary>
        /// <returns>A database connection</returns>
        DbConnection GetConnection();
        
        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// </summary>
        /// <returns>A database connection</returns>
        Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets an existing transaction; creates a new one from the connection if it does not exist and we support
        /// sharing of connections and transactions. You are responsible for committing the transaction.
        /// </summary>
        /// <returns>A database transaction</returns>
        DbTransaction GetTransaction();
        
        /// <summary>
        /// Gets an existing transaction; creates a new one from the connection if it does not exist and we support
        /// sharing of connections and transactions. You are responsible for committing the transaction.
        /// </summary>
        /// <returns>A database transaction</returns>
         Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Is there a transaction open?
        /// </summary>
        bool HasOpenTransaction { get; }

        /// <summary>
        /// Is there a shared connection? (Do we maintain state of just create anew)
        /// </summary>
        bool IsSharedConnection { get; }
    }
}
