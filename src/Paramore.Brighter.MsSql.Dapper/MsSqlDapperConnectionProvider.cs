using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Dapper;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MySql.Dapper
{
    public class MsSqlDapperConnectionProvider : IMsSqlTransactionConnectionProvider
    {
        private readonly IUnitOfWork _unitOfWork;

        public MsSqlDapperConnectionProvider(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public SqlConnection GetConnection()
        {
            return (SqlConnection)_unitOfWork.Database;
        }

        public Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<SqlConnection>();
            tcs.SetResult(GetConnection());
            return tcs.Task;
        }

        public SqlTransaction GetTransaction()
        {
            return (SqlTransaction)_unitOfWork.BeginOrGetTransaction();
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
