using System;
using System.Text;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageBodyTests;

public class MessageBodyEqualityTests
{
    [Fact]
    public void When_two_bodies_have_identical_bytes_should_be_equal()
    {
        // Arrange
        var payload = "{\"key\":\"value\"}";
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));

        // Act
        var result = body1.Equals(body2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void When_two_bodies_have_different_bytes_should_not_be_equal()
    {
        // Arrange
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{\"a\":1}")));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{\"b\":2}")));

        // Act
        var result = body1.Equals(body2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void When_comparing_with_null_should_not_be_equal()
    {
        // Arrange
        var body = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("test")));

        // Act
        var result = body.Equals(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void When_using_equality_operator_with_identical_bytes_should_be_equal()
    {
        // Arrange
        var payload = "hello world";
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));

        // Act & Assert
        Assert.True(body1 == body2);
    }

    [Fact]
    public void When_using_inequality_operator_with_different_bytes_should_not_be_equal()
    {
        // Arrange
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("foo")));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("bar")));

        // Act & Assert
        Assert.True(body1 != body2);
    }

    [Fact]
    public void When_two_bodies_are_equal_should_have_same_hash_code()
    {
        // Arrange
        var payload = "{\"key\":\"value\"}";
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));

        // Act & Assert
        Assert.Equal(body1.GetHashCode(), body2.GetHashCode());
    }
}
