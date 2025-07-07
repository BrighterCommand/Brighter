using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims.FileSystem;

public class LuggageStoreExistsAsyncTests 
{
    [Fact]
    public async Task When_checking_store_that_exists_async()
    {
        var pathName = $"brightertestbucket-{Guid.NewGuid()}";
        
        //arrange
        var luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{pathName}"));
        
        await luggageStore.EnsureStoreExistsAsync();

        //act
        luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{pathName}"));

        Assert.NotNull(luggageStore);
        
        //teardown
        Directory.Delete($"./{pathName}");
    }
    
    [Fact]
    public async Task When_checking_store_that_does_not_exist_async()
    {
        
        var pathName = $"brightertestbucket-{Guid.NewGuid()}";
        
        //act
         var doesNotExist = await Catch.ExceptionAsync(async () =>
         {
             var luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{pathName}")
             { 
                 Strategy = StorageStrategy.Validate
             });
             await luggageStore.EnsureStoreExistsAsync();
         });

         Assert.NotNull(doesNotExist);
         Assert.True(doesNotExist is InvalidOperationException);
    }
}
