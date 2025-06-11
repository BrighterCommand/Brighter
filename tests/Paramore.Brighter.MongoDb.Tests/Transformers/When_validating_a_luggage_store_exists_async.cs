using System;
using System.Threading.Tasks;
using Paramore.Brighter.Transformers.MongoGridFS;
using Paramore.Brighter.Transforms.Storage;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Transformers;

[Trait("Category", "MongoDb")]
public class LuggageStoreExistsAsyncTests 
{
    [Fact]
    public async Task When_checking_store_that_does_not_exist_async()
    {
        //act
         var doesNotExist = await Catch.ExceptionAsync(async () =>
             {
                 var bucketName = $"brightertestbucket-{Guid.NewGuid()}";
                 var luggageStore = new MongoDbLuggageStore(new MongoDbLuggageStoreOptions(
                     Configuration.ConnectionString, 
                     Configuration.DatabaseName, 
                     bucketName)
                 {
                     Strategy = StorageStrategy.Validate,
                 }); 

                 await luggageStore.EnsureStoreExistsAsync();
             });

         Assert.NotNull(doesNotExist);
         Assert.True(doesNotExist is InvalidOperationException);
    }
}
