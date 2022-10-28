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
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transforms.Transformers
{
    public class ClaimCheckTransformer: IAmAMessageTransformAsync
    {
        public const string CLAIM_CHECK = "Claim Check Header";
        private readonly IAmAStorageProviderAsync _store;
        private int _thresholdInKilobytes;

        /// <summary>
        /// A claim check moves the payload of a message, which when wrapping checks for a payloads that exceeds a certain threshold size, and inserts those
        /// above that value into storage, removing them from the payload, then inserts a claim check into the  message header's context bag. On unwrapping
        /// it retrieves the payload, via the claim check and replaces the payload.
        /// </summary>
        /// <param name="store">The storage to use for the payload</param>
        /// <param name="threshold">The size at which checking luggage is triggered. If size is 0 or less, the check will always be triggered</param>
        public ClaimCheckTransformer(IAmAStorageProviderAsync store)
        {
            _store = store;
        }
        
        public void Dispose()
        {
        }

        public void InitializeWrapFromAttributeParams(params object[] initializerList)
        {
            _thresholdInKilobytes = (int)initializerList[0];
        }

        public void InitializeUnwrapFromAttributeParams(params object[] initializerList)
        {
        }

        public async Task<Message> Wrap(Message message)
        {
            var body = message.Body.Value;
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            await writer.WriteAsync(body);
            await writer.FlushAsync();
            stream.Position = 0;

            var id = await _store.UploadAsync(stream);

            message.Header.Bag[CLAIM_CHECK] = id;
            message.Body = new MessageBody($"Claim Check {id}");
            return message;
        }

        public async Task<Message> Unwrap(Message message)
        {
            throw new NotImplementedException();
        }
    }
}
