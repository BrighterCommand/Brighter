#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

/// <summary>
/// A read-only <see cref="Stream"/> that wraps a <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/>,
/// providing zero-copy stream access to in-memory data.
/// </summary>
/// <remarks>
/// This avoids allocating a <see cref="MemoryStream"/> and copying data when a stream interface
/// is required over existing <see cref="ReadOnlyMemory{T}"/> content (e.g. for compression).
/// </remarks>
internal sealed class ReadOnlyMemoryStream : Stream
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyMemoryStream"/> class.
    /// </summary>
    /// <param name="memory">The <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/> to wrap.</param>
    public ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _position = 0;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _memory.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > _memory.Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            _position = (int)value;
        }
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = _memory.Length - _position;
        if (remaining <= 0) return 0;

        var bytesToRead = Math.Min(count, remaining);
        _memory.Span.Slice(_position, bytesToRead).CopyTo(buffer.AsSpan(offset, bytesToRead));
        _position += bytesToRead;
        return bytesToRead;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => (int)offset,
            SeekOrigin.Current => _position + (int)offset,
            SeekOrigin.End => _memory.Length + (int)offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0 || newPosition > _memory.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _position = newPosition;
        return _position;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read(buffer, offset, count));
    }

#if NETSTANDARD2_0
    // Stream.ReadAsync(Memory<byte>) override not available on netstandard2.0
#else
    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var remaining = _memory.Length - _position;
        if (remaining <= 0) return new ValueTask<int>(0);
        var bytesToRead = Math.Min(buffer.Length, remaining);
        _memory.Span.Slice(_position, bytesToRead).CopyTo(buffer.Span);
        _position += bytesToRead;
        return new ValueTask<int>(bytesToRead);
    }
#endif

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown. This stream is read-only.</exception>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown. This stream is read-only.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Flush() { }
}
