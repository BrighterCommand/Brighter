using System;
using System.IO;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Core.Tests.Claims.FileSystem;
public class LuggageStoreExistsAsyncTests
{
    [Test]
    public async Task When_checking_store_that_exists_async()
    {
        var pathName = $"brightertestbucket-{Guid.NewGuid()}";
        //arrange
        var luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{pathName}"));
        await luggageStore.EnsureStoreExistsAsync();
        //act
        luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{pathName}"));
        await Assert.That(luggageStore).IsNotNull();
        //teardown
        Directory.Delete($"./{pathName}");
    }

    [Test]
    public async Task When_checking_store_that_does_not_exist_async()
    {
        var pathName = $"brightertestbucket-{Guid.NewGuid()}";
        //act
        var doesNotExist = await Catch.ExceptionAsync(async () =>
        {
            var luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{pathName}") { Strategy = StorageStrategy.Validate });
            await luggageStore.EnsureStoreExistsAsync();
        });
        await Assert.That(doesNotExist).IsNotNull();
        await Assert.That(doesNotExist is InvalidOperationException).IsTrue();
    }
}