using System;
using System.Text;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageBodyTests;

public class MessageBodyReadOnlyMemoryConstructionTests
{
    [Fact]
    public void When_constructing_from_ReadOnlyMemory_should_not_copy()
    {
        // Arrange
        var jsonPayload = "{\"key\":\"value\"}";
        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        var memory = new ReadOnlyMemory<byte>(bytes);

        // Act
        var body = new MessageBody(memory);

        // Assert — Memory property returns the same ReadOnlyMemory that was passed in
        Assert.Equal(memory.Length, body.Memory.Length);
        Assert.True(memory.Span.SequenceEqual(body.Memory.Span));

        // Assert — the Memory property references the same underlying array (no copy)
        Assert.True(memory.Equals(body.Memory));
    }

    [Fact]
    public void When_constructing_from_ReadOnlyMemory_should_expose_bytes_for_backward_compat()
    {
        // Arrange
        var jsonPayload = "{\"key\":\"value\"}";
        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        var memory = new ReadOnlyMemory<byte>(bytes);

        // Act
        var body = new MessageBody(memory);

        // Assert — Bytes property returns a valid byte array with matching content
        Assert.Equal(bytes.Length, body.Bytes.Length);
        Assert.Equal(bytes, body.Bytes);
    }

    [Fact]
    public void When_constructing_from_ReadOnlyMemory_should_return_correct_value_string()
    {
        // Arrange
        var jsonPayload = "{\"key\":\"value\"}";
        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        var memory = new ReadOnlyMemory<byte>(bytes);

        // Act
        var body = new MessageBody(memory, characterEncoding: CharacterEncoding.UTF8);

        // Assert — Value returns the correct UTF-8 decoded string
        Assert.Equal(jsonPayload, body.Value);
    }
}
