using System;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims.FileSystem;

public class LuggageUploadMissingParametersTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void When_creating_luggagestore_missing_pathName(string? bucketName)
    {
        //arrange
        var exception = Catch.Exception(() => new FileSystemStorageProvider(new FileSystemOptions(bucketName!)));

        Assert.NotNull(exception);
        Assert.True(exception is ArgumentException);
    }
}
