using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Dapper;

namespace Paramore.Brighter.Sqlite.Dapper
{
    public class SqliteDapperConnectionProvider : ISqliteTransactionConnectionProvider 
    {
        private readonly IUnitOfWork _unitOfWork;

        public SqliteDapperConnectionProvider(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        
        public SqliteConnection GetConnection()
        {
            return (SqliteConnection)_unitOfWork.Database;
        }

        public Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<SqliteConnection>();
            tcs.SetResult(GetConnection());
            return tcs.Task;
        }

        public SqliteTransaction GetTransaction()
        {
            return (SqliteTransaction)_unitOfWork.BeginOrGetTransaction();
        }

        public bool HasOpenTransaction
        {
            get
            {
                return _unitOfWork.HasTransaction();
            }
        }

        public bool IsSharedConnection
        {
            get { return true; }

        }
    }
}
