using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using FluentAssertions;
using Paramore.Brighter.CommandStore.DynamoDB;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.CommandStore.DynamoDB
{
    public class DynamoDbCommandExistsAsyncTests : BaseCommandStoreDyamoDBBaseTest
    {       
        private readonly MyCommand _command;
        private readonly DynamoDbCommandStore _dynamoDbCommandStore;
        private readonly Guid _guid = Guid.NewGuid();
        private readonly string _contextKey;
    
        public DynamoDbCommandExistsAsyncTests()
        {                        
            _command = new MyCommand { Id = _guid, Value = "Test Earliest"};
            _contextKey = "test-context-key";

            var createTableRequest = new DynamoDbCommandStoreBuilder(DynamoDbTestHelper.DynamoDbCommandStoreTestConfiguration.TableName).CreateCommandStoreTableRequest();
        
            DynamoDbTestHelper.CreateCommandStoreTable(createTableRequest);
            _dynamoDbCommandStore = new DynamoDbCommandStore(DynamoDbTestHelper.DynamoDbContext, DynamoDbTestHelper.DynamoDbCommandStoreTestConfiguration);

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = DynamoDbTestHelper.DynamoDbCommandStoreTestConfiguration.TableName,
                ConsistentRead = false
            };
        
            var dbContext = DynamoDbTestHelper.DynamoDbContext;
            dbContext.SaveAsync(ConstructCommand(_command, DateTime.UtcNow, _contextKey), config).GetAwaiter().GetResult();
        }

        [Fact]
        public async Task When_checking_a_command_exist()
        {
            var commandExists = await _dynamoDbCommandStore.ExistsAsync<MyCommand>(_command.Id, _contextKey);

            commandExists.Should().BeTrue("because the command exists.", commandExists);
        }

        [Fact]
        public async Task When_checking_a_command_exist_for_a_different_context()
        {
            var commandExists = await _dynamoDbCommandStore.ExistsAsync<MyCommand>(_command.Id, _contextKey);

            commandExists.Should().BeFalse("because the command exists for a different context.", commandExists);
        }

        [Fact]
        public async Task When_checking_a_command_does_not_exist()
        {
            var commandExists = await _dynamoDbCommandStore.ExistsAsync<MyCommand>(Guid.Empty, _contextKey);

            commandExists.Should().BeFalse("because the command doesn't exists.", commandExists);
        }
    }
}
