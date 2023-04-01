using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Dapper;

namespace Paramore.Brighter.MySql.Dapper
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly SqlConnection _connection;
        private SqlTransaction _transaction;

        public UnitOfWork(DbConnectionStringProvider dbConnectionStringProvider)
        {
            _connection = new SqlConnection(dbConnectionStringProvider.ConnectionString);
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
                _transaction = _connection.BeginTransaction();
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
                try { _transaction.Rollback(); } catch (Exception) { /*can't check transaction status, so it will throw if already committed*/ }
            }
            
            if (_connection.State == ConnectionState.Open)
            {
                _connection.Close();
            }
        }
    }
}
