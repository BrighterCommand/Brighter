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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Paramore.Brighter.Transforms.Storage
{
    /// <summary>
    /// Provides in-memory storage of a message body, intended for use with testing only
    /// </summary>
    public class InMemoryStorageProviderAsync : IAmAStorageProviderAsync
    {
        private readonly Dictionary<Guid, string> _contents = new Dictionary<Guid, string>();       
        
        public async Task<Guid> UploadAsync(Stream stream)
        {
            var id = Guid.NewGuid();
            var reader = new StreamReader(stream);
            _contents.Add(id, await reader.ReadToEndAsync());
            return id;
        }

        public async Task<Stream> DownloadAsync(Guid claimCheck)
        {
            if (_contents.TryGetValue(claimCheck, out string value))
            {
                var stream = new MemoryStream();
                var writer = new StreamWriter(stream);
                await writer.WriteAsync(value);
                await writer.FlushAsync();
                stream.Position = 0;
                return stream;
            }

            throw new ArgumentOutOfRangeException("claimCheck", claimCheck, "Could not find the claim check in the store");
        }
    }
}
