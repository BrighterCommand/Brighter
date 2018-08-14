using System;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.CommandStore.DynamoDB;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.CommandStore.DynamoDB
{
    public class DynamoDbRangeOfCommandsTests
    {
        private readonly Guid[] _guids;
        private readonly DynamoDbCommand<MyCommand> _command2;
        private readonly DynamoDbCommandStore _dynamoDbCommandStore;
        private readonly DateTime _timeStamp = new DateTime(2018, 7, 5, 12, 0, 0);        
        
        public DynamoDbRangeOfCommandsTests()
        {            
            _guids = new[] {Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()};
            
            var commandEarliest = ConstructCommand(new MyCommand { Id = _guids[0], Value = "Test Earliest"}, _timeStamp.AddHours(-4));
            _command2 = ConstructCommand(new MyCommand { Id = _guids[1], Value = "Test Message 2"}, _timeStamp.AddHours(-2));
            var commandLatest = ConstructCommand(new MyCommand { Id = _guids[2], Value = "Test Latest"}, _timeStamp.AddHours(-1));
            var nonTopicCommand = ConstructCommand(new DifferentCommand { Id = _guids[3], Value = "Different Command "}, _timeStamp.AddHours(-2));

            var dynamoDbTestHelper = new DynamoDbTestHelper();
            var createTableRequest = new DynamoDbCommandStoreBuilder(dynamoDbTestHelper.DynamoDbCommandStoreTestConfiguration.TableName).CreateCommandStoreTableRequest();
            
            dynamoDbTestHelper.CreateCommandStoreTable(createTableRequest);
            _dynamoDbCommandStore = new DynamoDbCommandStore(dynamoDbTestHelper.DynamoDbContext, dynamoDbTestHelper.DynamoDbCommandStoreTestConfiguration);

            var dbContext = dynamoDbTestHelper.DynamoDbContext;
            dbContext.SaveAsync(commandEarliest).GetAwaiter().GetResult();
            dbContext.SaveAsync(_command2).GetAwaiter().GetResult();
            dbContext.SaveAsync(commandLatest).GetAwaiter().GetResult();
            dbContext.SaveAsync(nonTopicCommand).GetAwaiter().GetResult();
        }

        private DynamoDbCommand<T> ConstructCommand<T>(T command, DateTime timeStamp) where T : class, IRequest
        {                                               
            return new DynamoDbCommand<T>
            {
                CommandDate = $"{typeof(T).Name}+{timeStamp:yyyy-MM-dd}",
                Time = $"{timeStamp.Ticks}",
                CommandId = command.Id.ToString(),
                CommandType = typeof(T).Name,
                CommandBody = JsonConvert.SerializeObject(command),       
            };
        }        

        [Fact]
        public void When_reading_messages_by_numerical_range()
        {
            var retrievedMessages = _dynamoDbCommandStore.Get<MyCommand>(_timeStamp, _timeStamp.AddHours(-3), _timeStamp.AddHours(-2));
            
            //_should_read_the_last_two_messages_from_the_store
            retrievedMessages.Should().HaveCount(1);
            retrievedMessages.Single().Should().Be(_command2);                                  
        }        
        
        [Fact]
        public void When_reading_message_from_time()
        {
            var retrievedMessages = _dynamoDbCommandStore.Get<MyCommand>(_timeStamp, _timeStamp.AddHours(-2));

            //_should_read_the_last_two_messages_from_the_store
            retrievedMessages.Should().HaveCount(2);
            retrievedMessages.FirstOrDefault(m => m.Id == _guids[1]).Should().NotBeNull();
            retrievedMessages.FirstOrDefault(m => m.Id == _guids[2]).Should().NotBeNull();
        }
        
        [Fact]
        public void When_reading_message_until_time()
        {
            var retrievedMessages = _dynamoDbCommandStore.Get<MyCommand>(_timeStamp, endTime: _timeStamp.AddHours(-2));

            //_should_read_the_last_two_messages_from_the_store
            retrievedMessages.Should().HaveCount(2);
            retrievedMessages.FirstOrDefault(m => m.Id == _guids[0]).Should().NotBeNull();
            retrievedMessages.FirstOrDefault(m => m.Id == _guids[1]).Should().NotBeNull();
        }
    }

    internal class DifferentCommand : ICommand
    {
        public DifferentCommand()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; set; }

        public string Value { get; set; }
    }
}
