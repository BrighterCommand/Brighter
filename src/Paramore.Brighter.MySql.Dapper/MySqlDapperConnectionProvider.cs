using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Dapper;

namespace Paramore.Brighter.MySql.Dapper
{
    public class MySqlDapperConnectionProvider<T> : IMySqlTransactionConnectionProvider where T : UnitOfWork
    {
        private readonly T _unitOfWork;

        public MySqlDapperConnectionProvider(T unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        
        public MySqlConnection GetConnection()
        {
            _unitOfWork.GetConnection();
        }

        public async Task<MySqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _unitOfWork.GetConnectionAsync();
        }

        public MySqlTransaction GetTransaction()
        {
            _unitOfWork.GetTransaction();
        }

        public bool HasOpenTransaction { get; }
        public bool IsSharedConnection { get; }
    }
}
