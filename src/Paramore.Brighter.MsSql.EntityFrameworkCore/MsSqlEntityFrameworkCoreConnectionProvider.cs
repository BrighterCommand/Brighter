﻿using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Paramore.Brighter.MsSql.EntityFrameworkCore
{
    public class MsSqlEntityFrameworkCoreConnectionProvider<T> : IMsSqlConnectionProvider where T : DbContext
    {
        private readonly T _context;
        
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using the Database Connection from an Entity Framework Core DbContext.
        /// </summary>
        public MsSqlEntityFrameworkCoreConnectionProvider(T context)
        {
            _context = context;
        }
        
        public SqlConnection GetConnection()
        {
            return (SqlConnection)_context.Database.GetDbConnection();
        }

        public async Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetConnection();
        }

        public SqlTransaction GetTransaction()
        {
            var trans = (SqlTransaction)_context.Database.CurrentTransaction?.GetDbTransaction();
            return trans;
        }

        public bool HasOpenTransaction { get => _context.Database.CurrentTransaction != null; }
        public bool IsSharedConnection { get => true; }
    }
}
