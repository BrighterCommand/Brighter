using System;
using System.Threading.Tasks;
using Paramore.Brighter.Transformers.MongoGridFS;
using Paramore.Brighter.Transforms.Storage;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Transformers;

[Trait("Category", "MongoDb")]
public class LuggageStoreExistsTests 
{
    [Fact]
    public void When_checking_store_that_does_not_exist()
    {
        //act
         var doesNotExist = Catch.Exception(() =>
             {
                 var bucketName = $"brightertestbucket-{Guid.NewGuid()}";
                 var luggageStore = new MongoDbLuggageStore(new MongoDbLuggageStoreOptions(
                     Configuration.ConnectionString, 
                     Configuration.DatabaseName, 
                     bucketName)
                 {
                     Strategy = StorageStrategy.Validate,
                 }); 

                 luggageStore.EnsureStoreExists();
             });

         Assert.NotNull(doesNotExist);
         Assert.True(doesNotExist is InvalidOperationException);
    }
}
