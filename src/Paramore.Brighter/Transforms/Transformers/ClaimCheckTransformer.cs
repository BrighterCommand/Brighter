﻿#region Licence

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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transforms.Transformers
{
    public class ClaimCheckTransformer : IAmAMessageTransformAsync
    {
        public const string CLAIM_CHECK = "claim_check_header";
        private readonly IAmAStorageProviderAsync _store;
        private int _thresholdInBytes;
        private bool _retainLuggage;

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

        /// <summary>
        /// NOTE: Default constructor is not intended for usage, but is required for the service activator to not throw an exception
        /// due to missing dependencies when auto-registration registers this transformer but you do not use the claim check
        /// functionality
        /// If you forget to register your storage provider, you will get a not implemented exception when you try to use the
        /// claim check functionality
        /// </summary>
        public ClaimCheckTransformer()
        {
            _store = new NullLuggageStore();
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// We assume that the initializer list contains
        /// [0] - an integer representing the size of the threshold to convert to a string in Kb i.e. 5 would be 5Kb
        /// </summary>
        /// <param name="initializerList">The initialization for a claim check</param>
        public void InitializeWrapFromAttributeParams(params object[] initializerList)
        {
            if (initializerList.Length != 1) throw new ArgumentException("Missing parameter for threshold size", "initializerList");

            _thresholdInBytes = (int)initializerList[0] * 1024;
        }

        /// <summary>
        /// We assume the initializer list contains
        /// [0] -a bool, true if we should keep the message contents in storage, false if we should delete it
        /// </summary>
        /// <param name="initializerList"></param>
        public void InitializeUnwrapFromAttributeParams(params object[] initializerList)
        {
            if (initializerList.Length != 1) throw new ArgumentException("Missing parameter for luggage retention", "initializerList");

            _retainLuggage = (bool)initializerList[0];
        }

        /// <summary>
        /// If a message exceeds the threshold passed through the initializer list, then put it into storage
        /// If we place it in storage, set a header property to contain the 'claim' that can be used to retrieve the 'luggage'
        /// </summary>
        /// <param name="message">The message whose contents we want to </param>
        /// <param name="cancellationToken">Add cancellation token</param>
        /// <returns>The message, with 'luggage' swapped out if over the threshold</returns>
        public async Task<Message> WrapAsync(Message message, CancellationToken cancellationToken= default)
        {
            if (System.Text.Encoding.Unicode.GetByteCount(message.Body.Value) < _thresholdInBytes) return message;

            var body = message.Body.Value;
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            await writer.WriteAsync(body);
            await writer.FlushAsync();
            stream.Position = 0;

            var id = await _store.StoreAsync(stream, cancellationToken);

            message.Header.Bag[CLAIM_CHECK] = id;
            message.Body = new MessageBody($"Claim Check {id}");

            return message;
        }

        /// <summary>
        /// If a message has a 'claim' in its header, then retrieve the associated 'luggage' from the store and replace the body
        /// </summary>
        /// <param name="message">The message, with luggage retrieved if required</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken = default)
        {
            if (message.Header.Bag.TryGetValue(CLAIM_CHECK, out object objId))
            {
                var id = (string)objId;
                var luggage = await new StreamReader(await _store.RetrieveAsync(id, cancellationToken)).ReadToEndAsync();
                var newBody = new MessageBody(luggage);
                message.Body = newBody;
                if (!_retainLuggage)
                {
                    await _store.DeleteAsync(id, cancellationToken);
                    message.Header.Bag.Remove(CLAIM_CHECK);
                }
            }

            return message;
        }
    }
}
