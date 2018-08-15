using System;
using Amazon.DynamoDBv2.DataModel;
using FluentAssertions;
using Paramore.Brighter.CommandStore.DynamoDB;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.CommandStore.DynamoDB
{
    public class DynamoDbCommandExistsTests : BaseCommandStoreDyamoDBBaseTest
    {       
        private readonly MyCommand _command;
       
        private readonly DynamoDbCommandStore _dynamoDbCommandStore;
        private readonly Guid _guid = Guid.NewGuid();
        
        public DynamoDbCommandExistsTests()
        {                        
            _command = new MyCommand { Id = _guid, Value = "Test Earliest"};

            
            var createTableRequest = new DynamoDbCommandStoreBuilder(DynamoDbTestHelper.DynamoDbCommandStoreTestConfiguration.TableName).CreateCommandStoreTableRequest();
            
            DynamoDbTestHelper.CreateCommandStoreTable(createTableRequest);
            _dynamoDbCommandStore = new DynamoDbCommandStore(DynamoDbTestHelper.DynamoDbContext, DynamoDbTestHelper.DynamoDbCommandStoreTestConfiguration);

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = DynamoDbTestHelper.DynamoDbCommandStoreTestConfiguration.TableName,
                ConsistentRead = false
            };
            
            var dbContext = DynamoDbTestHelper.DynamoDbContext;
            dbContext.SaveAsync(ConstructCommand(_command, DateTime.UtcNow), config).GetAwaiter().GetResult();
        }

        [Fact]
        public void When_checking_a_command_exist()
        {
            var commandExists = _dynamoDbCommandStore.Exists<MyCommand>(_command.Id);

            commandExists.Should().BeTrue("because the command exists.", commandExists);
        }

        [Fact]
        public void When_checking_a_command_does_not_exist()
        {
            var commandExists = _dynamoDbCommandStore.Exists<MyCommand>(Guid.Empty);

            commandExists.Should().BeFalse("because the command doesn't exists.", commandExists);
        }
    }
}
