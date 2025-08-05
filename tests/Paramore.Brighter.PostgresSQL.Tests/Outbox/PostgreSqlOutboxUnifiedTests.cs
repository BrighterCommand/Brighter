using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox
{
    [Trait("Category", "PostgresSql")]
    public class PostgreSqlOutboxUnifiedTests : IDisposable
    {
        private readonly PostgresSqlTestHelper _postgresSqlTestHelper;
        private readonly PostgreSqlOutbox _unifiedOutbox;
        private readonly Message _message;

        public PostgreSqlOutboxUnifiedTests()
        {
            _postgresSqlTestHelper = new PostgresSqlTestHelper();
            _postgresSqlTestHelper.SetupMessageDb();

            _unifiedOutbox = new PostgreSqlOutbox(_postgresSqlTestHelper.OutboxConfiguration);
            _message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
        }

        [Fact]
        public void When_Using_Unified_Outbox_Sync_Operations_Should_Work()
        {
            // Arrange & Act
            _unifiedOutbox.Add(_message);
            var retrievedMessage = _unifiedOutbox.Get(_message.Id);

            // Assert
            retrievedMessage.Should().NotBeNull();
            retrievedMessage.Id.Should().Be(_message.Id);
            retrievedMessage.Header.Topic.Should().Be(_message.Header.Topic);
            retrievedMessage.Body.Value.Should().Be(_message.Body.Value);
        }

        [Fact]
        public async Task When_Using_Unified_Outbox_Async_Operations_Should_Work()
        {
            // Arrange & Act
            await _unifiedOutbox.AddAsync(_message);
            var retrievedMessage = await _unifiedOutbox.GetAsync(_message.Id);

            // Assert
            retrievedMessage.Should().NotBeNull();
            retrievedMessage.Id.Should().Be(_message.Id);
            retrievedMessage.Header.Topic.Should().Be(_message.Header.Topic);
            retrievedMessage.Body.Value.Should().Be(_message.Body.Value);
        }

        [Fact]
        public void When_Using_Unified_Outbox_Mixed_Sync_Async_Operations_Should_Work()
        {
            // Arrange - Add sync
            _unifiedOutbox.Add(_message);

            // Act - Get async
            var retrievedMessageAsync = _unifiedOutbox.GetAsync(_message.Id).Result;

            // Assert
            retrievedMessageAsync.Should().NotBeNull();
            retrievedMessageAsync.Id.Should().Be(_message.Id);
        }

        public void Dispose()
        {
            _postgresSqlTestHelper.CleanUpDb();
        }
    }
}
