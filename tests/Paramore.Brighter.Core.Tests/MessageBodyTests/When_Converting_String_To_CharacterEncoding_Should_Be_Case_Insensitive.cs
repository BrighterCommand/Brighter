using Paramore.Brighter.Extensions;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageBodyTests;

public class CharacterEncodingCaseInsensitiveTests
{
    [Theory]
    [InlineData("utf-8")]
    [InlineData("UTF-8")]
    [InlineData("Utf-8")]
    public void When_converting_utf8_variants_should_return_utf8(string input)
    {
        // Act
        var result = input.ToCharacterEncoding();

        // Assert
        Assert.Equal(CharacterEncoding.UTF8, result);
    }

    [Theory]
    [InlineData("us-ascii")]
    [InlineData("US-ASCII")]
    [InlineData("Us-Ascii")]
    public void When_converting_ascii_variants_should_return_ascii(string input)
    {
        // Act
        var result = input.ToCharacterEncoding();

        // Assert
        Assert.Equal(CharacterEncoding.ASCII, result);
    }

    [Theory]
    [InlineData("base64")]
    [InlineData("BASE64")]
    [InlineData("Base64")]
    public void When_converting_base64_variants_should_return_base64(string input)
    {
        // Act
        var result = input.ToCharacterEncoding();

        // Assert
        Assert.Equal(CharacterEncoding.Base64, result);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("UNKNOWN")]
    [InlineData("something-else")]
    public void When_converting_unknown_string_should_return_raw(string input)
    {
        // Act
        var result = input.ToCharacterEncoding();

        // Assert
        Assert.Equal(CharacterEncoding.Raw, result);
    }
}
