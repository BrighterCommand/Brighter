using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Transforms.Transformers
{
    public class CompressPayloadTransformer : IAmAMessageTransformAsync
    {
        private CompressionMethod _compressionMethod = CompressionMethod.GZip;
        private CompressionLevel _compressionLevel = CompressionLevel.Optimal;
        private int _thresholdInBytes;
        private string _contentType = "application/json";
        private const ushort GZIP_LEAD_BYTES = 0x8b1f;
        private const byte ZLIB_LEAD_BYTE = 0x78;

        public void Dispose() { }

        public void InitializeWrapFromAttributeParams(params object[] initializerList)
        {
            _compressionMethod = (CompressionMethod)initializerList[0];
            _compressionLevel = (CompressionLevel)initializerList[1];
            _thresholdInBytes = (int)initializerList[2] * 1024;

        }

        public void InitializeUnwrapFromAttributeParams(params object[] initializerList)
        {
            _compressionMethod = (CompressionMethod)initializerList[0];
            _contentType = (string)initializerList[1];
        }

        public async Task<Message> WrapAsync(Message message, CancellationToken cancellationToken = default)
        {
            var bytes = message.Body.Bytes;

            //don't transform it too small
            if (bytes.Length < _thresholdInBytes) return message;
            
            using var input = new MemoryStream(bytes);
            using var output = new MemoryStream();
            
            (Stream compressionStream, string mimeType) = CreateCompressionStream(output);
            await input.CopyToAsync(compressionStream);
            compressionStream.Close();

            message.Body = new MessageBody(output.ToArray(), mimeType);

            return message;
        }


        public async Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken = default)
        {
            if (!IsCompressed(message)) return message;
            
            var bytes = message.Body.Bytes;
            using var input = new MemoryStream(bytes);
            using var output = new MemoryStream();
            
            Stream deCompressionStream = CreateDecompressionStream(input);
            await deCompressionStream.CopyToAsync(output);
            deCompressionStream.Close();

            message.Body = new MessageBody(output.ToArray(), _contentType);

            return message;
        }
       
        private (Stream , string) CreateCompressionStream(MemoryStream uncompressed)
        {
            switch (_compressionMethod)
            {
                case CompressionMethod.GZip:
                    return (new GZipStream(uncompressed, _compressionLevel), "application/gzip");
#if NETSTANDARD2_0
                case CompressionMethod.Zlib:
                    throw new ArgumentException("Zlib is not supported in nestandard20");
                case CompressionMethod.Brotli:
                    throw new ArgumentException("Brotli is not supported in nestandard20");
#else
                case CompressionMethod.Zlib:
                    return (new ZLibStream(uncompressed, _compressionLevel), "application/deflate");  
                case CompressionMethod.Brotli:
                    return (new BrotliStream(uncompressed, _compressionLevel), "application/br");              
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
            switch (_compressionMethod)
            {
                case CompressionMethod.GZip:
                    return message.Body.ContentType == "application/gzip" && message.Body.Bytes.Length >= 2 && BitConverter.ToUInt16(message.Body.Bytes, 0) == GZIP_LEAD_BYTES;
                case CompressionMethod.Zlib:
                    return  message.Body.ContentType == "application/deflate" && message.Body.Bytes[0] == ZLIB_LEAD_BYTE; 
                case CompressionMethod.Brotli:
                    return message.Body.ContentType == "application/br";
                default:
                    return false;
                    
            }
        }

    }
}
