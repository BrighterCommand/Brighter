using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Dapper;

namespace Paramore.Brighter.MySql.Dapper
{
    public class MsSqlDapperConnectionProvider : IAmATransactionConnectonProvider 
    {
        private readonly IUnitOfWork _unitOfWork;

        public MsSqlDapperConnectionProvider(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        
        public DbConnection GetConnection()
        {
            return (SqlConnection)_unitOfWork.Database;
        }

        public Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<DbConnection>();
            tcs.SetResult(GetConnection());
            return tcs.Task;
        }

        public DbTransaction GetTransaction()
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
