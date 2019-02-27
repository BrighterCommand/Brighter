using System;
using Newtonsoft.Json;
using Paramore.Brighter.Inbox.DynamoDB;

namespace Paramore.Brighter.Tests.Inbox.DynamoDB
{
    public abstract class BaseImboxDyamoDBBaseTest : IDisposable
    {
        protected readonly DynamoDbTestHelper DynamoDbTestHelper;

        protected BaseImboxDyamoDBBaseTest()
        {
            DynamoDbTestHelper = new DynamoDbTestHelper();
        }
        
        protected DynamoDbCommand<T> ConstructCommand<T>(T command, DateTime timeStamp, string contextKey) where T : class, IRequest
        {
            return new DynamoDbCommand<T>
            {
                CommandDate = $"{typeof(T).Name}+{timeStamp:yyyy-MM-dd}",
                Time = $"{timeStamp.Ticks}",
                CommandId = command.Id.ToString(),
                CommandType = typeof(T).Name,
                CommandBody = JsonConvert.SerializeObject(command),
                ContextKey = contextKey
            };
        }
                
        public void Dispose()
        {
            DynamoDbTestHelper.CleanUpCommandDb();
        }
    }
}
