using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.PostgreSql
{
    public class PostgreSqlNpgsqlConnectionProvider : IPostgreSqlConnectionProvider
    {
        private readonly string _connectionString;

        public PostgreSqlNpgsqlConnectionProvider(RelationalDatabaseOutboxConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));

            _connectionString = configuration.ConnectionString;
        }

        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<NpgsqlConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(GetConnection());
            return await tcs.Task;
        }

        public NpgsqlTransaction GetTransaction()
        {
            //This connection factory does not support transactions
            return null;
        }

        public bool HasOpenTransaction { get => false; }

        public bool IsSharedConnection { get => false; }
    }
}
