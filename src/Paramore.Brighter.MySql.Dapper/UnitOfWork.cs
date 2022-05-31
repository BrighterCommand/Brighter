using System;
using System.Data;
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
                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }
                _transaction = _connection.BeginTransaction();
            }

            return _transaction;
        }

        public async Task<DbTransaction> BeginOrGetTransactionAsync(CancellationToken cancellationToken)
        {
            if (!HasTransaction())
            {
                if (_connection.State != ConnectionState.Open)
                {
                    await _connection.OpenAsync(cancellationToken);
                }
                _transaction = await _connection.BeginTransactionAsync(cancellationToken);
            }

            return _transaction;
        }

        public bool HasTransaction()
        {
            return _transaction != null;
        }

        public void Dispose()
        {
            if (_transaction != null)
            {
                //can't check transaction status, so it will throw if already committed
                try { _transaction.Rollback(); } catch (Exception) { }
            }
            
            if (_connection.State == ConnectionState.Open)
            {
                _connection.Close();
            }
        }
    }
}
