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
            await compressionStream.FlushAsync(cancellationToken);

            message.Body = new MessageBody(output.ToArray(), mimeType);

            return message;
        }


        public async Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken = default)
        {
            var bytes = message.Body.Bytes;
            using var input = new MemoryStream(bytes);
            using var output = new MemoryStream();
            
            Stream deCompressionStream = CreateDecompressionStream(input);
            await deCompressionStream.CopyToAsync(output);
            await input.FlushAsync(cancellationToken);

            message.Body = new MessageBody(output.ToArray(), _contentType);

            return message;
        }
       
        private (Stream , string) CreateCompressionStream(MemoryStream uncompressed)
        {
            switch (_compressionMethod)
            {
                case CompressionMethod.GZip:
                    return (new GZipStream(uncompressed, _compressionLevel), "application/gzip");
                case CompressionMethod.Deflate:
                    return (new DeflateStream(uncompressed, _compressionLevel), "application/x-deflate");
#if NETSTANDARD2_0
                case CompressionMethod.Brotli:
                    throw new ArgumentException("Brotli is not supported in nestandard20");
#else
                  case CompressionMethod.Brotli:
                    return (new BrotliStream(uncompressed, _compressionLevel), "pplication/x-brotli");              
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
                case CompressionMethod.Deflate:
                    return new DeflateStream(compressed, CompressionMode.Decompress);
#if NETSTANDARD2_0
                case CompressionMethod.Brotli:
                    throw new ArgumentException("Brotli is not supported in nestandard20");
#else
                  case CompressionMethod.Brotli:
                    return new BrotliStream(compressed, CompressionMode.Decompress);              
#endif
                default:
                    return compressed;
            }
        }

    }
}
