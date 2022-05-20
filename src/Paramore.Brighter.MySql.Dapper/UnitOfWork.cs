using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Dapper;

namespace Paramore.Brighter.MySql.Dapper
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly MySqlConnection _connection;
        private MySqlTransaction _transaction;

        public UnitOfWork(DbConnectionStringProvider dbConnectionStringProvider)
        {
            _connection = new MySqlConnection(dbConnectionStringProvider.ConnectionString);
        }

        public void Commit()
        {
            if (HasTransaction())
            {
                _transaction.Commit();
                _transaction = null;
            }
        }
        
        public DbConnection Database
        {
            get { return _connection; }
        }

        public DbTransaction BeginOrGetTransaction()
        {
            //ToDo: make this thread safe
            if (!HasTransaction())
            {
                _transaction = _connection.BeginTransaction();
            }

            return _transaction;
        }

        public async Task<DbTransaction> BeginOrGetTransactionAsync(CancellationToken cancellationToken)
        {
            if (!HasTransaction())
            {
                _transaction = await _connection.BeginTransactionAsync(cancellationToken);
            }

            return _transaction;
        }

        public bool HasTransaction()
        {
            return _transaction == null;
        }

        public void Dispose()
        {
            if (_transaction != null)
            {
                _transaction.Rollback();
            }
            _connection?.Close();
        }
    }
}
