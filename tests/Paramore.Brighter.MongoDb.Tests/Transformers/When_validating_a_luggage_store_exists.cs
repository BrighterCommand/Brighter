using System;
using System.Threading.Tasks;
using Paramore.Brighter.Transformers.MongoGridFS;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.MongoDb.Tests.Transformers;

[Category("MongoDb")]
public class LuggageStoreExistsTests 
{
    [Test]
    public async Task When_checking_store_that_does_not_exist()
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

         await Assert.That(doesNotExist).IsNotNull();
         await Assert.That(doesNotExist is InvalidOperationException).IsTrue();
    }
}
