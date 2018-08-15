using System;
using Newtonsoft.Json;
using Paramore.Brighter.CommandStore.DynamoDB;

namespace Paramore.Brighter.Tests.CommandStore.DynamoDB
{
    public abstract class BaseCommandStoreDyamoDBBaseTest : IDisposable
    {
        protected readonly DynamoDbTestHelper DynamoDbTestHelper;

        protected BaseCommandStoreDyamoDBBaseTest()
        {
            DynamoDbTestHelper = new DynamoDbTestHelper();
        }
        
        protected DynamoDbCommand<T> ConstructCommand<T>(T command, DateTime timeStamp) where T : class, IRequest
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
                
        public void Dispose()
        {
            DynamoDbTestHelper.CleanUpCommandDb();
        }
    }
}
