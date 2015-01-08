#region Licence
/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SqlServerCe;
using System.Threading.Tasks;
using Common.Logging;

namespace paramore.brighter.commandprocessor.messagestore.mssql
{
    public class MsSqlMessageStore : IAmAMessageStore<Message>
    {
        private readonly MsSqlMessageStoreConfiguration _configuration;
        private readonly ILog _log;

        public MsSqlMessageStore(MsSqlMessageStoreConfiguration configuration, ILog log)
        {
            _configuration = configuration;
            _log = log;
        }

        public async Task Add(Message message)
        {
            var sql = string.Format("INSERT INTO {0} (Id, MessageType, Topic, Body) VALUES (@Id, @MessageType, @Topic, @Body)", _configuration.MessageStoreTableName);
            var parameters = new[]
            {
                CreateSqlParameter("Id", message.Id), 
                CreateSqlParameter("MessageType", message.Header.MessageType.ToString()), 
                CreateSqlParameter("Topic", message.Header.Topic), 
                CreateSqlParameter("Body", message.Body.Value),
            };

            using (var connection = GetConnection())
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();

                command.CommandText = sql;
                command.Parameters.AddRange(parameters);
                await command.ExecuteNonQueryAsync();
            }
        }

        private DbParameter CreateSqlParameter(string parameterName, object value)
        {
            switch (_configuration.Type)
            {
                case MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer:
                    return new SqlParameter(parameterName, value);
                case MsSqlMessageStoreConfiguration.DatabaseType.SqlCe:
                    return new SqlCeParameter(parameterName, value);
            }
            return null;
        }

        private DbConnection GetConnection()
        {
            switch (_configuration.Type)
            {
                case MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer:
                    return new SqlConnection(_configuration.ConnectionString);
                case MsSqlMessageStoreConfiguration.DatabaseType.SqlCe:
                    return new SqlCeConnection(_configuration.ConnectionString);
            }
            return null;
        }

        public async Task<Message> Get(Guid messageId)
        {
            var sql = string.Format("SELECT Id, MessageType, Topic, Body FROM {0} WHERE Id = @Id", _configuration.MessageStoreTableName);
            var parameters = new[]
            {
                CreateSqlParameter("Id", messageId)
            };

            var result = await ExecuteCommand(async command => MapFunction(await command.ExecuteReaderAsync()), sql, parameters);
            return result;
        }

        private static Message MapFunction(IDataReader dr)
        {
            if (dr.Read())
            {
                var id = dr.GetGuid(dr.GetOrdinal("Id"));
                var messageType = (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
                var topic = dr.GetString(dr.GetOrdinal("Topic"));

                var header = new MessageHeader(id, topic, messageType);
                var body = new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

                return new Message(header, body);
            }

            return new Message();
        }

        private async Task<T> ExecuteCommand<T>(Func<DbCommand, Task<T>> execute, string sql, params DbParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                await connection.OpenAsync();
                T item = await execute(command);
                return item;
                
            }
        }
    }
}