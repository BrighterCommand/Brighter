using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Test.Helpers.Loggers
{
    /// <summary>
    /// Provides a pooled buffer writer for bytes, implementing <see cref="IBufferWriter{T}"/> and <see cref="IDisposable"/>.
    /// </summary>
    /// <remarks>
    /// This class is designed to efficiently manage a buffer of bytes using a pool to minimize allocations.
    /// It supports writing to the buffer, advancing the position, and retrieving the written data.
    /// </remarks>
    public class CorePooledByteBufferWriter : IBufferWriter<byte>, IDisposable
    {
        /// <summary>
        /// Represents the maximum buffer size that can be allocated by the <see cref="CorePooledByteBufferWriter"/>.
        /// </summary>
        /// <remarks>
        /// This value is derived from <c>Array.MaxLength</c> in the .NET runtime, ensuring that the buffer size does not exceed the maximum allowable length for arrays.
        /// </remarks>
        public const int MaximumBufferSize = 0X7FFFFFC7;
        private const int MinimumBufferSize = 256;

        // This class allows two possible configurations: if rentedBuffer is not null then
        // it can be used as an IBufferWriter and holds a buffer that should eventually be
        // returned to the shared pool. If rentedBuffer is null, then the instance is in a
        // cleared/disposed state and it must re-rent a buffer before it can be used again.
        private byte[]? _rentedBuffer;
        private int _index;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorePooledByteBufferWriter"/> class with the specified initial capacity.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the buffer to rent from the pool.</param>
        /// <remarks>
        /// The constructor ensures that the initial capacity is greater than zero and rents a buffer of the specified size from the shared pool.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="initialCapacity"/> is less than or equal to zero.</exception>
        public CorePooledByteBufferWriter(int initialCapacity)
            : this()
        {
            Debug.Assert(initialCapacity > 0, "initialCapacity > 0");

            _rentedBuffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
            _index = 0;
        }

        private CorePooledByteBufferWriter()
        {
#if !NETSTANDARD2_0_OR_GREATER
            // Ensure we are in sync with the Array.MaxLength implementation.
            Debug.Assert(Array.MaxLength == MaximumBufferSize, "Array.MaxLength == MaximumBufferSize");
#endif
        }

        /// <summary>
        /// Gets the memory region that has been written to.
        /// </summary>
        /// <value>
        /// A <see cref="ReadOnlyMemory{T}"/> of bytes representing the written portion of the buffer.
        /// </value>
        /// <remarks>
        /// This property returns a read-only view of the buffer that contains the data written so far.
        /// It is useful for retrieving the data without modifying the underlying buffer.
        /// </remarks>
        public ReadOnlyMemory<byte> WrittenMemory
        {
            get
            {
                Debug.Assert(_rentedBuffer is not null, "this._rentedBuffer != null");
                Debug.Assert(_index <= _rentedBuffer!.Length, "this._index <= this._rentedBuffer.Length");
                return _rentedBuffer.AsMemory(0, _index);
            }
        }

        /// <summary>
        /// Gets the number of bytes written to the buffer.
        /// </summary>
        /// <value>
        /// The total count of bytes that have been written to the buffer so far.
        /// </value>
        /// <remarks>
        /// This property returns the current position in the buffer, which represents the number of bytes written.
        /// </remarks>
        public int WrittenCount
        {
            get
            {
                Debug.Assert(_rentedBuffer is not null, "this._rentedBuffer is not null");
                return _index;
            }
        }

        /// <summary>
        /// Gets the total capacity of the underlying rented buffer.
        /// </summary>
        /// <value>
        /// The total capacity of the buffer in bytes.
        /// </value>
        /// <remarks>
        /// The capacity represents the total size of the buffer rented from the pool.
        /// It is the maximum amount of data that can be written to the buffer before it needs to be resized.
        /// </remarks>
        public int Capacity
        {
            get
            {
                Debug.Assert(_rentedBuffer is not null, "this._rentedBuffer is not null");
                return _rentedBuffer!.Length;
            }
        }

        /// <summary>
        /// Gets the amount of free capacity available in the rented buffer.
        /// </summary>
        /// <value>
        /// The number of bytes that can still be written to the buffer before it needs to be resized.
        /// </value>
        /// <remarks>
        /// This property calculates the difference between the total length of the rented buffer and the current position,
        /// indicating how much more data can be written without requiring a new buffer allocation.
        /// </remarks>
        public int FreeCapacity
        {
            get
            {
                Debug.Assert(_rentedBuffer is not null, "this._rentedBuffer is not null");
                return _rentedBuffer!.Length - _index;
            }
        }

        /// <summary>
        /// Creates an empty instance of the <see cref="CorePooledByteBufferWriter"/> class for caching purposes.
        /// </summary>
        /// <returns>A new instance of <see cref="CorePooledByteBufferWriter"/>.</returns>
        /// <remarks>
        /// This method is intended to be used for creating an instance of <see cref="CorePooledByteBufferWriter"/>
        /// that can be cached and reused to minimize allocations.
        /// </remarks>
        public static CorePooledByteBufferWriter CreateEmptyInstanceForCaching()
        {
            return new();
        }

        /// <summary>
        /// Clears the contents of the buffer, resetting the writer to its initial state.
        /// </summary>
        /// <remarks>
        /// This method clears the current buffer by resetting the internal index to zero and clearing the contents of the buffer.
        /// It does not return the buffer to the pool.
        /// </remarks>
        public void Clear()
        {
            ClearHelper();
        }

        /// <summary>
        /// Clears the current buffer and returns it to the shared pool.
        /// </summary>
        /// <remarks>
        /// This method ensures that the buffer is cleared and returned to the shared pool for reuse,
        /// effectively resetting the state of the <see cref="CorePooledByteBufferWriter"/> instance.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the buffer is already null.</exception>
        public void ClearAndReturnBuffers()
        {
            Debug.Assert(_rentedBuffer is not null, "this._rentedBuffer is not null");

            ClearHelper();
            byte[] toReturn = _rentedBuffer!;
            _rentedBuffer = null;
            ArrayPool<byte>.Shared.Return(toReturn);
        }

        /// <summary>
        /// Releases the resources used by the <see cref="CorePooledByteBufferWriter"/> instance.
        /// </summary>
        /// <remarks>
        /// This method returns the rented buffer back to the shared pool and sets the internal buffer to null.
        /// </remarks>
        public void Dispose()
        {
            if (_rentedBuffer is null)
            {
                return;
            }

            ClearHelper();
            byte[] toReturn = _rentedBuffer;
            _rentedBuffer = null;
            ArrayPool<byte>.Shared.Return(toReturn);
        }

        /// <summary>
        /// Initializes the current instance of the <see cref="CorePooledByteBufferWriter"/> class with a new buffer of the specified initial capacity.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the buffer to rent from the pool.</param>
        /// <remarks>
        /// This method is intended to reinitialize an existing instance of <see cref="CorePooledByteBufferWriter"/> that is in a cleared or disposed state.
        /// It ensures that the initial capacity is greater than zero and rents a buffer of the specified size from the shared pool.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="initialCapacity"/> is less than or equal to zero.</exception>
        public void InitializeEmptyInstance(int initialCapacity)
        {
            Debug.Assert(initialCapacity > 0, "initialCapacity > 0");
            Debug.Assert(_rentedBuffer is null, "this._rentedBuffer is null");

            _rentedBuffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
            _index = 0;
        }

        /// <summary>
        /// Advances the writer's position by the specified count of bytes.
        /// </summary>
        /// <param name="count">The number of bytes to advance the writer's position by. Must be non-negative.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative or exceeds the available capacity.</exception>
        /// <remarks>
        /// This method updates the internal index to reflect the new position in the buffer.
        /// It ensures that the buffer has sufficient capacity to accommodate the advance.
        /// </remarks>
        public void Advance(int count)
        {
            Debug.Assert(_rentedBuffer is not null, "this._rentedBuffer is not null");
            Debug.Assert(count >= 0, "count >= 0");
            Debug.Assert(_index <= _rentedBuffer!.Length - count, "this._index <= this._rentedBuffer.Length - count");
            _index += count;
        }

        /// <summary>
        /// Provides a <see cref="Memory{T}"/> of bytes to write to, ensuring there is enough space for the specified size hint.
        /// </summary>
        /// <param name="sizeHint">The minimum number of bytes required. If 0, a default minimum size is used.</param>
        /// <returns>A <see cref="Memory{T}"/> of bytes to write to.</returns>
        /// <remarks>
        /// This method checks the current buffer and resizes it if necessary to accommodate the specified size hint.
        /// </remarks>
        public Memory<byte> GetMemory(int sizeHint = MinimumBufferSize)
        {
            CheckAndResizeBuffer(sizeHint);
            return _rentedBuffer.AsMemory(_index);
        }

        /// <summary>
        /// Provides a span of bytes from the underlying rented buffer, ensuring that there is enough space for the specified size hint.
        /// </summary>
        /// <param name="sizeHint">The minimum size of the span requested. If the size hint is not specified, a default value is used.</param>
        /// <returns>A <see cref="Span{T}"/> of bytes from the underlying buffer.</returns>
        /// <remarks>
        /// This method checks the current buffer and resizes it if necessary to accommodate the requested size hint.
        /// The returned span starts from the current position in the buffer.
        /// </remarks>
        public Span<byte> GetSpan(int sizeHint = MinimumBufferSize)
        {
            CheckAndResizeBuffer(sizeHint);
            return _rentedBuffer.AsSpan(_index);
        }

#if NETCOREAPP
        /// <summary>
        /// Asynchronously writes the contents of the buffer to the specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="destination">The <see cref="Stream"/> to which the buffer will be written.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> that represents the asynchronous write operation.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the buffer has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the buffer is in an invalid state.</exception>
        internal ValueTask WriteToStreamAsync(Stream destination, CancellationToken cancellationToken)
        {
            return destination.WriteAsync(WrittenMemory, cancellationToken);
        }

        /// <summary>
        /// Writes the contents of the buffer to the specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="destination">The <see cref="Stream"/> to which the buffer will be written.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the buffer has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the buffer is in an invalid state.</exception>
        internal void WriteToStream(Stream destination)
        {
            destination.Write(WrittenMemory.Span);
        }
#else
        /// <summary>
        /// Asynchronously writes the contents of the buffer to the specified stream.
        /// </summary>
        /// <param name="destination">The stream to which the buffer contents will be written.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="destination"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the buffer has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the buffer is in an invalid state.</exception>
        internal Task WriteToStreamAsync(Stream destination, CancellationToken cancellationToken)
        {
            Debug.Assert(this._rentedBuffer is not null, "_rentedBuffer is not null");
            return destination.WriteAsync(this._rentedBuffer, 0, this._index, cancellationToken);
        }

        /// <summary>
        /// Writes the contents of the buffer to the specified stream.
        /// </summary>
        /// <param name="destination">The stream to which the buffer contents will be written.</param>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="destination"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the buffer has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the buffer is in an invalid state.</exception>
        internal void WriteToStream(Stream destination)
        {
            Debug.Assert(this._rentedBuffer is not null, "_rentedBuffer is not null");
            destination.Write(this._rentedBuffer!, 0, this._index);
        }
#endif

        private void CheckAndResizeBuffer(int sizeHint)
        {
            Debug.Assert(_rentedBuffer is not null, "this._rentedBuffer is not null");
            Debug.Assert(sizeHint > 0, "sizeHint > 0");

            int currentLength = _rentedBuffer!.Length;
            int availableSpace = currentLength - _index;

            // If we've reached ~1GB written, grow to the maximum buffer
            // length to avoid incessant minimal growths causing perf issues.
            if (_index >= MaximumBufferSize / 2)
            {
                sizeHint = Math.Max(sizeHint, MaximumBufferSize - currentLength);
            }

            if (sizeHint > availableSpace)
            {
                int growBy = Math.Max(sizeHint, currentLength);

                int newSize = currentLength + growBy;

                if ((uint)newSize > MaximumBufferSize)
                {
                    newSize = currentLength + sizeHint;
                    if ((uint)newSize > MaximumBufferSize)
                    {
                        ThrowHelper.ThrowOutOfMemoryException_BufferMaximumSizeExceeded((uint)newSize);
                    }
                }

                byte[] oldBuffer = _rentedBuffer;

                _rentedBuffer = ArrayPool<byte>.Shared.Rent(newSize);

                Debug.Assert(oldBuffer.Length >= _index, "oldBuffer.Length >= this._index");
                Debug.Assert(_rentedBuffer.Length >= _index, "this._rentedBuffer.Length >= this._index");

                Span<byte> oldBufferAsSpan = oldBuffer.AsSpan(0, _index);
                oldBufferAsSpan.CopyTo(_rentedBuffer);
                oldBufferAsSpan.Clear();
                ArrayPool<byte>.Shared.Return(oldBuffer);
            }

            Debug.Assert(_rentedBuffer.Length - _index > 0, "this._rentedBuffer.Length - this._index > 0");
            Debug.Assert(_rentedBuffer.Length - _index >= sizeHint, "this._rentedBuffer.Length - this._index >= sizeHint");
        }

        private void ClearHelper()
        {
            Debug.Assert(_rentedBuffer is not null, "this._rentedBuffer is not null");
            Debug.Assert(_index <= _rentedBuffer!.Length, "this._index <= this._rentedBuffer.Length");

            _rentedBuffer.AsSpan(0, _index).Clear();
            _index = 0;
        }

        /// <summary>
        /// Provides helper methods for throwing exceptions within the <see cref="CorePooledByteBufferWriter"/> class.
        /// </summary>
        /// <remarks>
        /// This class contains methods that throw specific exceptions used by the <see cref="CorePooledByteBufferWriter"/>
        /// to handle error conditions, such as exceeding the maximum buffer size.
        /// </remarks>
        internal static partial class ThrowHelper
        {
            /// <summary>
            /// Throws an <see cref="OutOfMemoryException"/> when the buffer exceeds the maximum allowed size.
            /// </summary>
            /// <param name="capacity">The capacity that was attempted to be allocated, which exceeds the maximum buffer size.</param>
            /// <exception cref="OutOfMemoryException">Thrown when the buffer exceeds the maximum allowed size.</exception>
            /// <remarks>
            /// This method is used internally by the <see cref="CorePooledByteBufferWriter"/> class to handle scenarios where
            /// an attempt is made to allocate a buffer larger than the maximum allowed size.
            /// </remarks>
            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void ThrowOutOfMemoryException_BufferMaximumSizeExceeded(uint capacity)
            {
                throw new OutOfMemoryException($"Buffer exceeded maximum capacity of {capacity}");
            }
        }
    }
}
