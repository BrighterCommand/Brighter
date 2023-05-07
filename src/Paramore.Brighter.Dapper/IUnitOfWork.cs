using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Dapper
{
    /// <summary>
    /// Creates a unit of work, so that Brighter can access the active transaction for the Outbox
    /// </summary>
    public interface IUnitOfWork : IAmABoxTransactionProvider, IDisposable
    {
        /// <summary>
        /// Begins a new transaction against the database. Will open the connection if it is not already open,
        /// </summary>
        /// <returns>A transaction</returns>
        DbTransaction BeginOrGetTransaction();
        
        /// <summary>
        /// Begins a new transaction asynchronously against the database. Will open the connection if it is not already open,
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>A transaction</returns>
        Task<DbTransaction> BeginOrGetTransactionAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Commits any pending transactions
        /// </summary>
        void Commit();
        
        /// <summary>
        /// The .NET DbConnection to the Database
        /// </summary>
        DbConnection Database { get; }
        
        /// <summary>
        /// Is there an extant transaction
        /// </summary>
        /// <returns>True if a transaction is already open on this unit of work, false otherwise</returns>
        bool HasTransaction();
    }
}
