using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.Fakes
{
    internal class FakeTransactionProvider<T> : RelationalDbTransactionProvider where T : class
    {
        private readonly T _context;
        
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using the Database Connection from an Entity Framework Core DbContext.
        /// </summary>
        public FakeTransactionProvider(T context)
        {
            _context = context;
        }
        
        /// <summary>
        /// Commit the transaction
        /// </summary>
        public override void Commit()
        {
            return;
        }
        
        /// <summary>
        /// Commit the transaction
        /// </summary>
        /// <returns>An awaitable Task</returns>
        public override Task CommitAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// Opens the connection if it is not opened 
        /// </summary>
        /// <returns>A database connection</returns>
        public override DbConnection GetConnection()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// The base class just returns a new or existing connection, but derived types may perform async i/o
        /// </summary>
        /// <returns>A database connection</returns>
        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an existing transaction; creates a new one from the connection if it does not exist.
        /// You should use the commit transaction using the Commit method. 
        /// </summary>
        /// <returns>A database transaction</returns>
        public override DbTransaction GetTransaction()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Rolls back a transaction
        /// </summary>
        public override async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }


        public override bool HasOpenTransaction => throw new NotImplementedException();

        public override bool IsSharedConnection => true;
    }
}
