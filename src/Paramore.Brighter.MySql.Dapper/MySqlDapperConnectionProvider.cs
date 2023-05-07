using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Dapper;

namespace Paramore.Brighter.MySql.Dapper
{
    public class MySqlDapperConnectionProvider : IAmATransactionConnectonProvider 
    {
        private readonly IUnitOfWork _unitOfWork;

        public MySqlDapperConnectionProvider(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        
        public DbConnection GetConnection()
        {
            return (MySqlConnection)_unitOfWork.Database;
        }

        public Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<DbConnection>();
            tcs.SetResult(GetConnection());
            return tcs.Task;
        }

        public DbTransaction GetTransaction()
        {
            return (MySqlTransaction)_unitOfWork.BeginOrGetTransaction();
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
