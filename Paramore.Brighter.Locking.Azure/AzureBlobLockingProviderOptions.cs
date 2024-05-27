#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Azure.Core;

namespace Paramore.Brighter.Locking.Azure;

public class AzureBlobLockingProviderOptions(
    Uri blobContainerUri,
    TokenCredential tokenCredential
    )
{
    /// <summary>
    /// The URI of the blob container
    /// </summary>
    public Uri BlobContainerUri { get; init; } = blobContainerUri;

    /// <summary>
    /// The Credential to use when writing blobs
    /// </summary>
    public TokenCredential TokenCredential { get; init; } = tokenCredential;

    /// <summary>
    /// The amount of time before the lease automatically expires
    /// </summary>
    public TimeSpan LeaseValidity { get; init; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// The function to provide the location to store the locks inside of the Blob container
    /// </summary>
    public Func<string, string> StorageLocationFunc = (resource) => $"lock-{resource}";
}
