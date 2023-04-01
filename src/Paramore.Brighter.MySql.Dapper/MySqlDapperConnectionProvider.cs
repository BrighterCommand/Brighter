using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Dapper;

namespace Paramore.Brighter.MySql.Dapper
{
    public class MySqlDapperConnectionProvider : IMySqlTransactionConnectionProvider
    {
        private readonly IUnitOfWork _unitOfWork;

        public MySqlDapperConnectionProvider(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public MySqlConnection GetConnection()
        {
            return (MySqlConnection)_unitOfWork.Database;
        }

        public Task<MySqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<MySqlConnection>();
            tcs.SetResult(GetConnection());
            return tcs.Task;
        }

        public MySqlTransaction GetTransaction()
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
