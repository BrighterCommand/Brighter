using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<MsSqlMessageQueue<T>>);
        private readonly MsSqlMessagingGatewayConfiguration _configuration;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlMessageQueue{T}" /> class.
        /// </summary>
        /// <param name="configuration"></param>
        public MsSqlMessageQueue(MsSqlMessagingGatewayConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (Logger.Value.IsDebugEnabled())
                Logger.Value.Debug(
                    $"MsSqlMessageQueue({_configuration.ConnectionString}, {_configuration.QueueStoreTable})");
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the orginating synchronization context if you need to access
        ///     thread specific storage
        ///     such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Add the passed message to the Queue
        /// </summary>
        /// <param name="message">The Message</param>
        /// <param name="topic">The topic name</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        public void Send(T message, string topic, int timeoutInMilliseconds = -1)
        {
            if (Logger.Value.IsDebugEnabled()) Logger.Value.Debug($"Send<{typeof(T).FullName}>(..., {topic})");

            var parameters = InitAddDbParameters(topic, message);

            using (var connection = GetConnection())
            {
                connection.Open();
                var sqlCmd = InitAddDbCommand(timeoutInMilliseconds, connection, parameters);
                sqlCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        ///     Add the passed message to the Queue (Async)
        /// </summary>
        /// <param name="message">The Message</param>
        /// <param name="topic">The topic name</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken">The active CancellationToken</param>
        /// <returns></returns>
        public async Task SendAsync(T message, string topic, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (Logger.Value.IsDebugEnabled()) Logger.Value.Debug($"SendAsync<{typeof(T).FullName}>(..., {topic})");

            var parameters = InitAddDbParameters(topic, message);

            using (var connection = GetConnection())
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                var sqlCmd = InitAddDbCommand(timeoutInMilliseconds, connection, parameters);
                await sqlCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
        }

        /// <summary>
        ///     Try receiving a message
        /// </summary>
        /// <param name="topic">The topic name</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns>The message received -or- ReceivedResult&lt;T&gt;.Empty when no message arrives within the timeout period</returns>
        public ReceivedResult<T> TryReceive(string topic, int timeoutInMilliseconds)
        {
            if (Logger.Value.IsDebugEnabled())
                Logger.Value.Debug($"TryReceive<{typeof(T).FullName}>(..., {timeoutInMilliseconds})");
            var rc = TryReceive(topic);
            var timeleft = timeoutInMilliseconds;
            while (!rc.IsDataValid && timeleft > 0)
            {
                Task.Delay(RetryDelay).Wait();
                timeleft -= RetryDelay;
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
            if (Logger.Value.IsDebugEnabled()) Logger.Value.Debug($"TryReceive<{typeof(T).FullName}>(...)");

            var parameters = InitRemoveDbParameters(topic);

            using (var connection = GetConnection())
            {
                connection.Open();
                var sqlCmd = InitRemoveDbCommand(connection, parameters);
                var reader = sqlCmd.ExecuteReader();
                if (!reader.Read())
                    return ReceivedResult<T>.Empty;
                var json = (string) reader[0];
                var messageType = (string) reader[1];
                var id = (long) reader[3];
                var contractResolver = new MessageDefaultContractResolver();
                var settings = new JsonSerializerSettings {ContractResolver = contractResolver};
                var message = JsonConvert.DeserializeObject<T>(json, settings);
                return new ReceivedResult<T>(true, json, topic, messageType, id, message);
            }
        }

        /// <summary>
        ///     Try receiving a message (Async)
        /// </summary>
        /// <param name="topic">The topic name</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The message received -or- ReceivedResult&lt;T&gt;.Empty when no message is waiting</returns>
        public async Task<ReceivedResult<T>> TryReceiveAsync(string topic,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (Logger.Value.IsDebugEnabled()) Logger.Value.Debug($"TryReceiveAsync<{typeof(T).FullName}>(...)");

            var parameters = InitRemoveDbParameters(topic);

            using (var connection = GetConnection())
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                var sqlCmd = InitRemoveDbCommand(connection, parameters);
                var reader = await sqlCmd.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
                if (!reader.Read())
                    return ReceivedResult<T>.Empty;
                var json = (string) reader[0];
                var messageType = (string) reader[1];
                var id = (int) reader[3];
                var contractResolver = new MessageDefaultContractResolver();
                var settings = new JsonSerializerSettings {ContractResolver = contractResolver};
                var message = JsonConvert.DeserializeObject<T>(json, settings);
                return new ReceivedResult<T>(true, json, topic, messageType, id, message);
            }
        }

        public bool IsMessageReady(string topic)
        {
            return NumberOfMessageReady(topic) > 0;
        }

        public int NumberOfMessageReady(string topic)
        {
            var sql = $"select COUNT(*) from Queues where Topic='{topic}'";
            using (var connection = GetConnection())
            {
                var sqlCmd = connection.CreateCommand();
                sqlCmd.CommandText = sql;
                return (int) sqlCmd.ExecuteScalar();
            }
        }

        /// <summary>
        ///     Purge all messages from the Queue
        /// </summary>
        public void Purge()
        {
            if (Logger.Value.IsDebugEnabled()) Logger.Value.Debug("Purge()");

            using (var connection = GetConnection())
            {
                connection.Open();
                var sqlCmd = InitPurgeDbCommand(connection);
                sqlCmd.ExecuteNonQuery();
            }
        }

        private DbConnection GetConnection() => new SqlConnection(_configuration.ConnectionString);

        private static DbParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqlParameter(parameterName, value);
        }

        private static DbParameter[] InitAddDbParameters(string topic, T message)
        {
            var parameters = new[]
            {
                CreateSqlParameter("topic", topic),
                CreateSqlParameter("messageType", typeof(T).FullName),
                CreateSqlParameter("payload", JsonConvert.SerializeObject(message))
            };
            return parameters;
        }

        private DbCommand InitAddDbCommand(int timeoutInMilliseconds, DbConnection connection, DbParameter[] parameters)
        {
            var sql =
                $"set nocount on;insert into {_configuration.QueueStoreTable} (Topic, MessageType, Payload) values(@topic, @messageType, @payload);";
            var sqlCmd = connection.CreateCommand();
            if (timeoutInMilliseconds != -1) sqlCmd.CommandTimeout = timeoutInMilliseconds;

            sqlCmd.CommandText = sql;
            sqlCmd.Parameters.AddRange(parameters);
            return sqlCmd;
        }

        private static DbParameter[] InitRemoveDbParameters(string topic)
        {
            var parameters = new[]
            {
                CreateSqlParameter("topic", topic)
            };
            return parameters;
        }

        private DbCommand InitRemoveDbCommand(DbConnection connection, DbParameter[] parameters)
        {
            var sql =
                $"set nocount on;with cte as (select top(1) Payload, MessageType, Topic, Id from {_configuration.QueueStoreTable}" +
                " with (rowlock, readpast) where Topic = @topic order by Id) delete from cte output deleted.Payload, deleted.MessageType, deleted.Topic, deleted.Id;";
            var sqlCmd = connection.CreateCommand();
            sqlCmd.CommandText = sql;
            sqlCmd.Parameters.AddRange(parameters);
            return sqlCmd;
        }

        private DbCommand InitPurgeDbCommand(DbConnection connection)
        {
            var sql = $"delete from from {_configuration.QueueStoreTable}";
            var sqlCmd = connection.CreateCommand();
            sqlCmd.CommandText = sql;
            return sqlCmd;
        }
    }
}