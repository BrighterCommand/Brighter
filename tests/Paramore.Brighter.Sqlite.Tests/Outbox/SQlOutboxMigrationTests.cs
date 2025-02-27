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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox
{
    [Trait("Category", "Sqlite")]
    public class SQlOutboxMigrationTests : IAsyncDisposable
    {
        private readonly SqliteOutbox _sqlOutbox;
        private readonly Message _message;
        private Message _storedMessage;
        private readonly SqliteTestHelper _sqliteTestHelper;

        public SQlOutboxMigrationTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _sqlOutbox  = new SqliteOutbox(new RelationalDatabaseConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.OutboxTableName));

            _message = new Message(new MessageHeader(
                Guid.NewGuid().ToString(), 
                new RoutingKey("test_topic"), 
                MessageType.MT_DOCUMENT), 
                new MessageBody("message body")
                );
            AddHistoricMessage(_message);
        }

        private void AddHistoricMessage(Message message)
        {
            var sql = string.Format("INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @HeaderBag, @Body)", _sqliteTestHelper.OutboxTableName);
            var parameters = new[]
            {
                new SqliteParameter("MessageId", message.Id.ToString()),
                new SqliteParameter("MessageType", message.Header.MessageType.ToString()),
                new SqliteParameter("Topic", message.Header.Topic.Value),
                new SqliteParameter("Timestamp", SqliteType.Text) { Value =message.Header.TimeStamp.ToString("s")},
                new SqliteParameter("HeaderBag",SqliteType.Text) { Value = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options)},
                new SqliteParameter("Body", message.Body.Value),
            };

            using var connection = new SqliteConnection(_sqliteTestHelper.ConnectionString);
            using var command = connection.CreateCommand();
            connection.Open();

            command.CommandText = sql;
            //command.Parameters.AddRange(parameters); used to work... but can't with current Sqlite lib. Iterator issue
            for (var index = 0; index < parameters.Length; index++)
            {
                command.Parameters.Add(parameters[index]);
            }
            command.ExecuteNonQuery();
        }

        [Fact]
        public void When_writing_a_message_with_minimal_header_information_to_the_outbox()
        {
            _storedMessage = _sqlOutbox.Get(_message.Id, new RequestContext());

            //_should_read_the_message_from_the__sql_outbox
            Assert.Equal(_message.Body.Value, _storedMessage.Body.Value);
            //_should_read_the_message_header_type_from_the__sql_outbox
            Assert.Equal(_message.Header.MessageType, _storedMessage.Header.MessageType);
            //_should_read_the_message_header_topic_from_the__sql_outbox
            Assert.Equal(_message.Header.Topic, _storedMessage.Header.Topic);
            //_should_default_the_timestamp_from_the__sql_outbox
            _storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss").Should()
                .Be(_message.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"));
            //_should_read_empty_header_bag_from_the__sql_outbox
            Assert.Empty(_storedMessage.Header.Bag.Keys ?? []);
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
