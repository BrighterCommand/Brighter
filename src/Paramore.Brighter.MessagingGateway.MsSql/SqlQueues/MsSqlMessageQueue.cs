using System;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MsSql.SqlQueues
{
    /// <summary>
    ///     Class MsSqlMessageQueue{T}
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MsSqlMessageQueue<T>
    {
        private const int RetryDelay = 100;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageQueue<T>>();
        private readonly RelationalDatabaseConfiguration _configuration;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlMessageQueue{T}" /> class.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="connectionProvider"></param>
        public MsSqlMessageQueue(RelationalDatabaseConfiguration configuration, IAmARelationalDbConnectionProvider  connectionProvider)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionProvider = connectionProvider;
            if (s_logger.IsEnabled(LogLevel.Debug))
                s_logger.LogDebug("MsSqlMessageQueue({ConnectionString}, {QueueStoreTable})", _configuration.ConnectionString, _configuration.QueueStoreTable);
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the originating synchronization context if you need to access
        ///     thread specific storage
        ///     such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Add the passed message to the Queue
        /// </summary>
        /// <param name="message">The Message</param>
        /// <param name="topic">The topic name</param>
        /// <param name="timeOut">Timeout for the send operation; MsSQL commands use seconds for timeout; -1 or null for default MSSQL timeout</param>
        public void Send(T message, RoutingKey topic, TimeSpan? timeOut = null)
        {
            timeOut ??= TimeSpan.FromMilliseconds(-1);
            
            if (s_logger.IsEnabled(LogLevel.Debug)) s_logger.LogDebug("Send<{CommandType}>(..., {Topic})", typeof(T).FullName, topic);

            var parameters = InitAddDbParameters(topic.Value, message);

            using var connection = _connectionProvider.GetConnection();
            var sqlCmd = InitAddDbCommand(timeOut.Value, connection, parameters);
            sqlCmd.ExecuteNonQuery();
        }

        /// <summary>
        ///     Add the passed message to the Queue (Async)
        /// </summary>
        /// <param name="message">The Message</param>
        /// <param name="topic">The topic name</param>
        /// <param name="timeOut">Timeout for the send operation; MsSQL commands use seconds for timeout; -1 or null for default MSSQL timeout</param>
        /// /// <param name="cancellationToken">The active CancellationToken</param>
        /// <returns></returns>
        public async Task SendAsync(T message, string topic, TimeSpan? timeOut, CancellationToken cancellationToken = default)
        {
            if (s_logger.IsEnabled(LogLevel.Debug)) s_logger.LogDebug("SendAsync<{CommandType}>(..., {Topic})", typeof(T).FullName, topic);

            timeOut ??= TimeSpan.FromMilliseconds(-1);
            
            var parameters = InitAddDbParameters(topic, message);

            using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            var sqlCmd = InitAddDbCommand(timeOut.Value, connection, parameters);
            await sqlCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        ///   Try receiving a message
        ///    Sync over Async
        /// </summary>
        /// <param name="topic">The topic name</param>
        /// <param name="timeOut">Timeout for reading a message of the queue; -1 or null for default timeout</param>
        /// <returns>The message received -or- ReceivedResult&lt;T&gt;.Empty when no message arrives within the timeout period</returns>
        public ReceivedResult<T> TryReceive(string topic, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromMilliseconds(-1);
            
            if (s_logger.IsEnabled(LogLevel.Debug))
                s_logger.LogDebug("TryReceive<{CommandType}>(..., {Timeout})", typeof(T).FullName, timeout.Value.TotalMilliseconds);
            
            var rc = TryReceive(topic);
            var timeLeft = timeout.Value.TotalMilliseconds;
            while (!rc.IsDataValid && timeLeft > 0)
            {
                Task.Delay(RetryDelay)
                    .GetAwaiter()
                    .GetResult();
                timeLeft -= RetryDelay;
                rc = TryReceive(topic);
            }

            return rc;
        }

        /// <summary>
        ///     Try receiving a message
        /// </summary>
        /// <param name="topic">The topic name</param>
        /// <returns>The message received -or- ReceivedResult&lt;T&gt;.Empty when no message is waiting</returns>
        private ReceivedResult<T> TryReceive(string topic)
        {
            if (s_logger.IsEnabled(LogLevel.Debug)) s_logger.LogDebug("TryReceive<{CommandType}>(...)", typeof(T).FullName);

            var parameters = InitRemoveDbParameters(topic);

            using var connection = _connectionProvider.GetConnection();
            var sqlCmd = InitRemoveDbCommand(connection, parameters);
            var reader = sqlCmd.ExecuteReader();
            if (!reader.Read())
                return ReceivedResult<T>.Empty;
            var json = (string) reader[0];
            var messageType = (string) reader[1];
            var id = (long) reader[3];
            var message = JsonSerializer.Deserialize<T>(json, JsonSerialisationOptions.Options);
            return new ReceivedResult<T>(true, json, topic, messageType, id, message);
        }

        /// <summary>
        ///     Try receiving a message (Async)
        /// </summary>
        /// <param name="topic">The topic name</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The message received -or- ReceivedResult&lt;T&gt;.Empty when no message is waiting</returns>
        public async Task<ReceivedResult<T>> TryReceiveAsync(string topic,
            CancellationToken cancellationToken = default)
        {
            if (s_logger.IsEnabled(LogLevel.Debug)) s_logger.LogDebug("TryReceiveAsync<{CommandType}>(...)", typeof(T).FullName);

            var parameters = InitRemoveDbParameters(topic);

            using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            var sqlCmd = InitRemoveDbCommand(connection, parameters);
            var reader = await sqlCmd.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            if (!await reader.ReadAsync(cancellationToken))
                return ReceivedResult<T>.Empty;
            var json = (string) reader[0];
            var messageType = (string) reader[1];
            var id = (int) reader[3];
            var message = JsonSerializer.Deserialize<T>(json, JsonSerialisationOptions.Options);
            return new ReceivedResult<T>(true, json, topic, messageType, id, message);
        }

        public bool IsMessageReady(string topic)
        {
            return NumberOfMessageReady(topic) > 0;
        }

        public int NumberOfMessageReady(string topic)
        {
            var sql = $"select COUNT(*) from [{_configuration.QueueStoreTable}] where Topic='{topic}'";
            using var connection = _connectionProvider.GetConnection();
            var sqlCmd = connection.CreateCommand();
            sqlCmd.CommandText = sql;
            object? count = sqlCmd.ExecuteScalar();
            
            if (count is null) return 0;
            
            return (int) count;
        }

        /// <summary>
        ///     Purge all messages from the Queue
        /// </summary>
        public void Purge()
        {
            if (s_logger.IsEnabled(LogLevel.Debug)) s_logger.LogDebug("Purge()");

            using var connection = _connectionProvider.GetConnection();
            var sqlCmd = InitPurgeDbCommand(connection);
            sqlCmd.ExecuteNonQuery();
        }
        
        private static IDbDataParameter CreateDbDataParameter(string parameterName, object value)
        {
            return new SqlParameter(parameterName, value);
        }

        private static IDbDataParameter[] InitAddDbParameters(string topic, T message)
        {
            string? fullName = typeof(T).FullName;
            //not sure how we would ever get here.
            if (fullName is null) throw new ArgumentNullException(nameof(fullName), "MsSQLMessageQueue: The type of the message must have a full name");
           
            var parameters = new[]
            {
                CreateDbDataParameter("topic", topic),
                CreateDbDataParameter("messageType", fullName),
                CreateDbDataParameter("payload", JsonSerializer.Serialize(message, JsonSerialisationOptions.Options))
            };
            return parameters;
        }

        private DbCommand InitAddDbCommand(TimeSpan timeOut, DbConnection connection, IDbDataParameter[] parameters)
        {
            var sql =
                $"set nocount on;insert into [{_configuration.QueueStoreTable}] (Topic, MessageType, Payload) values(@topic, @messageType, @payload);";
            var sqlCmd = connection.CreateCommand();
            if (timeOut != TimeSpan.FromSeconds(-1)) sqlCmd.CommandTimeout = timeOut.Seconds;

            sqlCmd.CommandText = sql;
            sqlCmd.Parameters.AddRange(parameters);
            return sqlCmd;
        }

        private static IDbDataParameter[] InitRemoveDbParameters(string topic)
        {
            var parameters = new[]
            {
                CreateDbDataParameter("topic", topic)
            };
            return parameters;
        }

        private DbCommand InitRemoveDbCommand(DbConnection connection, IDbDataParameter[] parameters)
        {
            var sql =
                $"set nocount on;with cte as (select top(1) Payload, MessageType, Topic, Id from [{_configuration.QueueStoreTable}]" +
                " with (rowlock, readpast) where Topic = @topic order by Id) delete from cte output deleted.Payload, deleted.MessageType, deleted.Topic, deleted.Id;";
            var sqlCmd = connection.CreateCommand();
            sqlCmd.CommandText = sql;
            sqlCmd.Parameters.AddRange(parameters);
            return sqlCmd;
        }

        private DbCommand InitPurgeDbCommand(DbConnection connection)
        {
            var sql = $"delete from [{_configuration.QueueStoreTable}]";
            var sqlCmd = connection.CreateCommand();
            sqlCmd.CommandText = sql;
            return sqlCmd;
        }
    }
}
