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
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Transforms.Attributes;

/// <summary>
/// Decompresses a compressed payload.
/// Assumes that the payload is compressed with a supported <see cref="CompressionMethod"/> and that the type
/// for that compression method is indicated by the <see cref="MessageBody"/> BodyType, passed in via the message headers,
/// as this is used to figure out if the content is compressed or not. (The content may have fallen below a
/// compression threshold and be the original content type
/// </summary>
public class DecompressAttribute: UnwrapWithAttribute
{
    private readonly CompressionMethod _compressionMethod;

    /// <summary>
    /// Configures decompression of a payload
    /// </summary>
    /// <param name="step">The order to run this step in</param>
    /// <param name="compressionMethod">The <see cref="CompressionMethod"/> used to compress the payload. Supported decompression is Gzip, Zlib and Brotli. Defaults to GZip</param>
    public DecompressAttribute(
        int step,
        CompressionMethod compressionMethod = CompressionMethod.GZip) : base(step)
    {
        _compressionMethod = compressionMethod;
    }
        
    /// <summary>
    /// Passes the parameters used to configure this attribute to the runtime type that implements it
    /// </summary>
    /// <returns>Configuration values</returns>
    public override object[] InitializerParams()
    {
        return [_compressionMethod];
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
