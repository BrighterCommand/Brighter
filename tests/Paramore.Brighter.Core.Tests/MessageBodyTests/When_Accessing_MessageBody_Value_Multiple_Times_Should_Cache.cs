using System;
using System.Text;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageBodyTests;

public class MessageBodyValueCachingTests
{
    [Fact]
    public void When_accessing_Value_multiple_times_should_return_same_reference()
    {
        // Arrange
        var jsonPayload = "{\"key\":\"value\"}";
        var body = new MessageBody(jsonPayload);

        // Act
        var firstAccess = body.Value;
        var secondAccess = body.Value;

        // Assert — same string reference returned (cached, not recomputed)
        Assert.True(ReferenceEquals(firstAccess, secondAccess));
    }

    [Fact]
    public void When_accessing_Value_from_byte_array_should_cache_result()
    {
        // Arrange
        var jsonPayload = "{\"key\":\"value\"}";
        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        var body = new MessageBody(bytes, characterEncoding: CharacterEncoding.UTF8);

        // Act
        var firstAccess = body.Value;
        var secondAccess = body.Value;

        // Assert — correct value and same reference
        Assert.Equal(jsonPayload, firstAccess);
        Assert.True(ReferenceEquals(firstAccess, secondAccess));
    }

    [Fact]
    public void When_accessing_Value_with_ascii_encoding_should_cache_result()
    {
        // Arrange
        var payload = "hello world";
        var body = new MessageBody(payload, characterEncoding: CharacterEncoding.ASCII);

        // Act
        var firstAccess = body.Value;
        var secondAccess = body.Value;

        // Assert
        Assert.Equal(payload, firstAccess);
        Assert.True(ReferenceEquals(firstAccess, secondAccess));
    }

    [Fact]
    public void When_accessing_Value_with_base64_encoding_should_cache_result()
    {
        // Arrange
        var originalBytes = Encoding.UTF8.GetBytes("hello world");
        var base64String = Convert.ToBase64String(originalBytes);
        var body = new MessageBody(base64String, characterEncoding: CharacterEncoding.Base64);

        // Act
        var firstAccess = body.Value;
        var secondAccess = body.Value;

        // Assert
        Assert.Equal(base64String, firstAccess);
        Assert.True(ReferenceEquals(firstAccess, secondAccess));
    }
}
