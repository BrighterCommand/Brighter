using System;
using System.IO;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims.FileSystem;

public class LuggageStoreExistsTests 
{
    [Fact]
    public void When_checking_store_that_exists()
    {
        var pathName = $"brightertestbucket-{Guid.NewGuid()}";
        
        //arrange
        var luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{pathName}"));
        
        luggageStore.EnsureStoreExists();

        //act
        luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{pathName}"));

        Assert.NotNull(luggageStore);
        
        //teardown
        Directory.Delete($"./{pathName}");
    }
    
    [Fact]
    public void When_checking_store_that_does_not_exist_async()
    {
        
        var pathName = $"brightertestbucket-{Guid.NewGuid()}";
        
        //act
         var doesNotExist = Catch.Exception(() =>
         {
             var luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{pathName}")
             {
                 Strategy = StorageStrategy.Validate
             });
             
             luggageStore.EnsureStoreExists();
         });

         Assert.NotNull(doesNotExist);
         Assert.True(doesNotExist is InvalidOperationException);
    }
}
