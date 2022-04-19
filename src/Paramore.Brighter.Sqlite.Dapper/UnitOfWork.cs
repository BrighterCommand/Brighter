using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Dapper;
using SQLitePCL;

namespace Paramore.Brighter.Sqlite.Dapper
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly SqliteConnection _connection;
        private SqliteTransaction _transaction;

        public UnitOfWork(string dbConnectionString)
        {
            _connection = new SqliteConnection(dbConnectionString);
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
                _transaction = _connection.BeginTransaction();
            }

            return _transaction;
        }
        
        public Task<DbTransaction> BeginOrGetTransactionAsync(CancellationToken cancellationToken)
        {
            //NOTE: Sqlite does not support async begin transaction, so we fake it
            var tcs = new TaskCompletionSource<DbTransaction>();
            if (!HasTransaction())
            {
                _transaction = _connection.BeginTransaction();
            }
            
            tcs.SetResult(_transaction);

            return tcs.Task;
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
