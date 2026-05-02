using System;
using System.IO;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageBodyTests;

public class ReadOnlyMemoryStreamTests
{
    [Fact]
    public void When_reading_should_return_correct_bytes()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new ReadOnlyMemoryStream(new ReadOnlyMemory<byte>(data));
        var buffer = new byte[5];

        // Act
        var bytesRead = stream.Read(buffer, 0, buffer.Length);

        // Assert
        Assert.Equal(5, bytesRead);
        Assert.Equal(data, buffer);
    }

    [Fact]
    public void When_checking_length_should_match_memory_length()
    {
        // Arrange
        var data = new byte[] { 10, 20, 30 };
        var stream = new ReadOnlyMemoryStream(new ReadOnlyMemory<byte>(data));

        // Act & Assert
        Assert.Equal(3, stream.Length);
    }

    [Fact]
    public void When_reading_should_advance_position()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new ReadOnlyMemoryStream(new ReadOnlyMemory<byte>(data));
        var buffer = new byte[3];

        // Act
        stream.Read(buffer, 0, 3);

        // Assert
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void When_seeking_should_reset_position_and_read_correctly()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new ReadOnlyMemoryStream(new ReadOnlyMemory<byte>(data));
        var buffer = new byte[5];
        stream.Read(buffer, 0, 5);

        // Act
        stream.Seek(0, SeekOrigin.Begin);
        var buffer2 = new byte[5];
        var bytesRead = stream.Read(buffer2, 0, 5);

        // Assert
        Assert.Equal(5, bytesRead);
        Assert.Equal(data, buffer2);
    }

    [Fact]
    public void When_checking_capabilities_should_be_readable_and_seekable_but_not_writable()
    {
        // Arrange
        var stream = new ReadOnlyMemoryStream(new ReadOnlyMemory<byte>(new byte[] { 1 }));

        // Act & Assert
        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void When_writing_should_throw_not_supported()
    {
        // Arrange
        var stream = new ReadOnlyMemoryStream(new ReadOnlyMemory<byte>(new byte[] { 1 }));

        // Act & Assert
        var ex = Record.Exception(() => stream.Write(new byte[] { 1 }, 0, 1));
        Assert.IsType<NotSupportedException>(ex);
    }

    [Fact]
    public void When_reading_past_end_should_return_zero_bytes()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3 };
        var stream = new ReadOnlyMemoryStream(new ReadOnlyMemory<byte>(data));
        var buffer = new byte[3];
        stream.Read(buffer, 0, 3); // read all

        // Act
        var bytesRead = stream.Read(buffer, 0, 3);

        // Assert
        Assert.Equal(0, bytesRead);
    }
}
