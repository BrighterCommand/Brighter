using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Paramore.Brighter.MsSql.EntityFrameworkCore
{
    public class MsSqlEntityFrameworkCoreConnectionProvider<T> : RelationalDbConnectionProvider, IAmATransactionConnectionProvider where T : DbContext
    {
        private readonly T _context;
        
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using the Database Connection from an Entity Framework Core DbContext.
        /// </summary>
        public MsSqlEntityFrameworkCoreConnectionProvider(T context)
        {
            _context = context;
        }
        
        public override DbConnection GetConnection()
        {
            //This line ensure that the connection has been initialised and that any required interceptors have been run before getting the connection
            _context.Database.CanConnect();
            return _context.Database.GetDbConnection();
        }

        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            //This line ensure that the connection has been initialised and that any required interceptors have been run before getting the connection
            await _context.Database.CanConnectAsync(cancellationToken);
            return _context.Database.GetDbConnection();
        }

        public override DbTransaction GetTransaction()
        {
            var trans = (SqlTransaction)_context.Database.CurrentTransaction?.GetDbTransaction();
            return trans;
        }

        public override bool HasOpenTransaction { get => _context.Database.CurrentTransaction != null; }
        public override bool IsSharedConnection { get => true; }
    }
}
