using System;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Core.Tests.Claims.FileSystem;
public class LuggageUploadMissingParametersTests
{
    [Test]
    [Arguments("")]
    [Arguments(null)]
    public async Task When_creating_luggagestore_missing_pathName(string? bucketName)
    {
        //arrange
        var exception = Catch.Exception(() => new FileSystemStorageProvider(new FileSystemOptions(bucketName!)));
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception is ArgumentException).IsTrue();
    }
}