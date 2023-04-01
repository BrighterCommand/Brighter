using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Dapper;

namespace Paramore.Brighter.Sqlite.Dapper
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly SqliteConnection _connection;
        private SqliteTransaction _transaction;

        public UnitOfWork(DbConnectionStringProvider dbConnectionStringProvider)
        {                     
            _connection = new SqliteConnection(dbConnectionStringProvider.ConnectionString);
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

        /// <summary>
        /// Begins a transaction, if one not already started. Closes connection if required
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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
            if (HasTransaction())
            {
                //will throw if transaction completed, but no way to check transaction state via api
                try { _transaction.Rollback(); } catch (Exception) { }
            }

            if (_connection is { State: ConnectionState.Open })
            {
                _connection.Close();
            }
        }
    }
}
