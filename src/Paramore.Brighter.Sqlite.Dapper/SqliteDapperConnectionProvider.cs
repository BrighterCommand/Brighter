using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Dapper;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Sqlite.Dapper
{
    public class SqliteDapperConnectionProvider : RelationalDbConnectionProvider 
    {
        private readonly IUnitOfWork _unitOfWork;

        public SqliteDapperConnectionProvider(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        
        public override DbConnection GetConnection()
        {
            return (SqliteConnection)_unitOfWork.Database;
        }

        public IDbTransaction GetTransaction()
        {
            return (DbTransaction)_unitOfWork.BeginOrGetTransaction();
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
