#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.IO;
using System.IO.Compression;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Transforms.Transformers
{
    public class CompressPayloadTransformerAsync : IAmAMessageTransformAsync
    {
        private CompressionMethod _compressionMethod = CompressionMethod.GZip;
        private CompressionLevel _compressionLevel = CompressionLevel.Optimal;
        private int _thresholdInBytes;
        private const ushort GZIP_LEAD_BYTES = 0x8b1f;
        private const byte ZLIB_LEAD_BYTE = 0x78;
        
        /// <summary>Compression method GZip</summary>
        public const string GZIP = "application/gzip";
        /// <summary> Compression method Deflate</summary>
        public const string DEFLATE = "application/deflate";
        /// <summary> Compression method Brotli</summary>
        public const string BROTLI = "application/br";
        
        /// <summary> Original content type header name</summary>
        public const string ORIGINAL_CONTENTTYPE_HEADER = "originalContentType";
        
        /// <summary>
        /// Gets or sets the context. Usually the context is given to you by the pipeline and you do not need to set this
        /// </summary>
        /// <value>The context.</value>
        public IRequestContext? Context { get; set; }

        /// <summary>
        /// Dispose of this transform; a no-op
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// Initializes from the <see cref="TransformAttribute"/> wrap attribute parameters.
        /// Used to pass the compression algorithm to the transformer and the size over which to compress
        /// </summary>
        /// <param name="initializerList"></param>
        public void InitializeWrapFromAttributeParams(params object?[] initializerList)
        {
            _compressionMethod = (CompressionMethod)initializerList[0]!;
            _compressionLevel = (CompressionLevel)initializerList[1]!;
            _thresholdInBytes = (int)initializerList[2]! * 1024;

        }

        /// <summary>
        /// Initializes from the <see cref="TransformAttribute"/> unwrap attribute parameters.
        /// Used to pass the decompression algorithm to the transformer 
        /// </summary>
        /// <param name="initializerList"></param>
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
        {
            _compressionMethod = (CompressionMethod)initializerList[0]!;
        }

        /// <summary>
        /// Compress the message given the supplied compression algorithm
        /// </summary>
        /// <param name="message">The message to compress</param>
        /// <param name="publication">The publication for the channel that the message is being published to; useful for metadata</param>
        /// <param name="cancellationToken">Support for cancelling the operation</param>
        /// <returns>A message with a compressed body</returns>
        public async Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken = default)
        {
            var bytes = message.Body.Bytes;

            //don't transform it too small
            if (bytes.Length < _thresholdInBytes) return message;
            
            using var input = new MemoryStream(bytes);
            using var output = new MemoryStream();
            
            (Stream compressionStream, string mimeType) = CreateCompressionStream(output);
            await input.CopyToAsync(compressionStream);
            compressionStream.Close();

            message.Header.Bag.Add(ORIGINAL_CONTENTTYPE_HEADER, message.Body.ContentType is not null ? message.Body.ContentType.ToString() : MediaTypeNames.Text.Plain);
            var contentType = new ContentType(mimeType);
            var charset = message.Header.ContentType?.CharSet;
            if (!string.IsNullOrEmpty(charset)) contentType.CharSet = charset;
            message.Header.ContentType = contentType;
            message.Body.ContentType = contentType;
            message.Body = new MessageBody(output.ToArray(), contentType, CharacterEncoding.Raw);

            return message;
        }

        /// <summary>
        /// Decompress a message given the supplied compression algorithm
        /// </summary>
        /// <param name="message">The message to decompress</param>
        /// <param name="cancellationToken">Suppot for cancelling the operation</param>
        /// <returns>An uncompressed message</returns>
        public async Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken = default)
        {
            if (!IsCompressed(message)) return message;
            
            var bytes = message.Body.Bytes;
            using var input = new MemoryStream(bytes);
            using var output = new MemoryStream();
            
            Stream deCompressionStream = CreateDecompressionStream(input);
            await deCompressionStream.CopyToAsync(output);
            deCompressionStream.Close();

            string originalContentType = (string)message.Header.Bag[ORIGINAL_CONTENTTYPE_HEADER];
            var contentType = new ContentType(originalContentType) ;
            message.Body = new MessageBody(output.ToArray(), contentType);
            message.Header.ContentType = contentType;

            return message;
        }
       
        private (Stream , string) CreateCompressionStream(MemoryStream uncompressed)
        {
            switch (_compressionMethod)
            {
                case CompressionMethod.GZip:
                    return (new GZipStream(uncompressed, _compressionLevel), GZIP);
#if NETSTANDARD2_0
                case CompressionMethod.Zlib:
                    throw new ArgumentException("Zlib is not supported in nestandard20");
                case CompressionMethod.Brotli:
                    throw new ArgumentException("Brotli is not supported in nestandard20");
#else
                case CompressionMethod.Zlib:
                    return (new ZLibStream(uncompressed, _compressionLevel), DEFLATE);  
                case CompressionMethod.Brotli:
                    return (new BrotliStream(uncompressed, _compressionLevel), BROTLI);              
#endif
                default:
                    return (uncompressed, "application/json");
            }
        }
        
        private Stream CreateDecompressionStream(MemoryStream compressed)
        {
            switch (_compressionMethod)
            {
                case CompressionMethod.GZip:
                    return new GZipStream(compressed, CompressionMode.Decompress);

#if NETSTANDARD2_0
                case CompressionMethod.Zlib:
                    throw new ArgumentException("Zlib is not supported in nestandard20");
                case CompressionMethod.Brotli:
                    throw new ArgumentException("Brotli is not supported in nestandard20");
#else
                case CompressionMethod.Zlib:
                    return new ZLibStream(compressed, CompressionMode.Decompress); 
                case CompressionMethod.Brotli:
                    return new BrotliStream(compressed, CompressionMode.Decompress);              
#endif
                default:
                    return compressed;
            }
        }

        private bool IsCompressed(Message message)
        {
            if (message.Header.ContentType is null)
                return false;
            
            switch (_compressionMethod)
            {
                case CompressionMethod.GZip:
                    return message.Header.ContentType.ToString() == "application/gzip" && message.Body.Bytes.Length >= 2 && BitConverter.ToUInt16(message.Body.Bytes, 0) == GZIP_LEAD_BYTES;
                case CompressionMethod.Zlib:
                    return  message.Header.ContentType.ToString() == "application/deflate" && message.Body.Bytes[0] == ZLIB_LEAD_BYTE; 
                case CompressionMethod.Brotli:
                    return message.Header.ContentType.ToString() == "application/br";
                default:
                    return false;
                    
            }
        }

    }
}
