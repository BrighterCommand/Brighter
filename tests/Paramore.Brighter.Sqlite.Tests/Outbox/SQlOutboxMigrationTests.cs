using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.JsonConverters;
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
            _sqlOutbox  = new SqliteOutbox(new RelationalDatabaseConfiguration(_sqliteTestHelper.ConnectionString, outBoxTableName: _sqliteTestHelper.OutboxTableName));

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

            //Should read the message from the sql outbox
            Assert.Equal(_message.Body.Value, _storedMessage.Body.Value);
            //Should read the message header type from the sql outbox
            Assert.Equal(_message.Header.MessageType, _storedMessage.Header.MessageType);
            //Should read the message header topic from the sql outbox
            Assert.Equal(_message.Header.Topic, _storedMessage.Header.Topic);
            //Should default the timestamp from the sql outbox
            Assert.Equal(
                _message.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                _storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss")
            );
            //Should read empty header bag from the sql outbox
            Assert.Empty(_storedMessage.Header.Bag.Keys);
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
