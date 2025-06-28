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
using System.IO.Compression;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Transforms.Attributes
{
    /// <summary>
    /// Compresses the payload of a message to reduce its size
    /// </summary>
    public class CompressAttribute : WrapWithAttribute
    {
        private readonly CompressionMethod _compressionMethod;
        private readonly CompressionLevel _compressionLevel;
        private readonly int _thresholdInKb;

        /// <summary>
        /// Configures compression for a message
        /// </summary>
        /// <param name="step">The order in which to perform compression</param>
        /// <param name="compressionMethod">The compression method> One of <see cref="CompressionMethod"/>: GZip, Zlib, or Brotli. Defaults to Gzip </param>
        /// <param name="compressionLevel">The level of compression <see cref="CompressionLevel"/></param>
        /// <param name="thresholdInKb">How large the payload should be before we try to compress it.</param>
        public CompressAttribute(
            int step, 
            CompressionMethod compressionMethod = CompressionMethod.GZip, 
            CompressionLevel compressionLevel = CompressionLevel.Optimal, 
            int thresholdInKb = 0) : base(step)
        {
            _compressionMethod = compressionMethod;
            _compressionLevel = compressionLevel;
            _thresholdInKb = thresholdInKb;
        }
        
        /// <summary>
        /// Passes the parameters used to configure this attribute to the runtime type that implements it
        /// </summary>
        /// <returns>Configuration values</returns>
        public override object[] InitializerParams()
        {
            return [_compressionMethod, _compressionLevel, _thresholdInKb];
        }

        /// <summary>
        /// The runtime type that implements this middleware step
        /// </summary>
        /// <returns>The type for the compression middleware</returns>
        public override Type GetHandlerType()
        {
            return typeof(CompressPayloadTransformer);
        }
    }
}
