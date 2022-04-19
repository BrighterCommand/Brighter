using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.Sqlite.Dapper
{
    public class SqliteDapperConnectionProvider : ISqliteTransactionConnectionProvider 
    {
        private readonly UnitOfWork _unitOfWork;

        public SqliteDapperConnectionProvider(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        
        public SqliteConnection GetConnection()
        {
            return (SqliteConnection)_unitOfWork.Database;
        }

        public Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
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
