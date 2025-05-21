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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transforms.Transformers;

/// <summary>
/// Moves the payload of a message to storage, when the payload exceeds a certain threshold size, and inserts a
/// claim check into the message header
/// Obeys <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/extensions/dataref.md">Cloud Events</see>
/// specification of Claim Check for interoperability - this is a breaking change from V9.X.X
/// </summary>
public class ClaimCheckTransformer : IAmAMessageTransform, IAmAMessageTransformAsync
{
    public const string CLAIM_CHECK = "claim_check_header";
        
    private readonly IAmAStorageProvider _store;
    private readonly IAmAStorageProviderAsync _storeAsync;
    private int _thresholdInBytes;
    private bool _retainLuggage;
        
    /// <summary>
    /// Gets or sets the context. Usually the context is given to you by the pipeline and you do not need to set this
    /// </summary>
    /// <value>The context.</value>
    public IRequestContext? Context { get; set; }

    /// <summary>
    /// A claim check moves the payload of a message, which when wrapping checks for a payloads that exceeds a certain threshold size, and inserts those
    /// above that value into storage, removing them from the payload, then inserts a claim check into the  message header's context bag. On unwrapping
    /// it retrieves the payload, via the claim check and replaces the payload.
    /// </summary>
    /// <param name="store">The storage to use for the payload</param>
    /// <param name="storeAsync">The storage to use for the payload.</param>
    public ClaimCheckTransformer(IAmAStorageProvider store, IAmAStorageProviderAsync storeAsync)
    {
        _store = store;
        _storeAsync = storeAsync;
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
        _storeAsync = new NullLuggageStoreAsync();
    }

    public void Dispose()
    {
    }


    /// <summary>
    /// We assume that the initializer list contains
    /// [0] - an integer representing the size of the threshold to convert to a string in Kb i.e. 5 would be 5Kb
    /// </summary>
    /// <param name="initializerList">The initialization for a claim check</param>
    public void InitializeWrapFromAttributeParams(params object?[] initializerList)
    {
        if (initializerList.Length != 1)
        {
            throw new ArgumentException("Missing parameter for threshold size", nameof(initializerList));
        }

        _thresholdInBytes = (int)initializerList[0]! * 1024;
    }

    /// <summary>
    /// We assume the initializer list contains
    /// [0] -a bool, true if we should keep the message contents in storage, false if we should delete it
    /// </summary>
    /// <param name="initializerList"></param>
    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
    {
        if (initializerList.Length != 1)
        {
            throw new ArgumentException("Missing parameter for luggage retention", nameof(initializerList));
        }

        _retainLuggage = (bool)initializerList[0]!;
    }
        
    /// <summary>
    /// If a message exceeds the threshold passed through the initializer list, then put it into storage
    /// If we place it in storage, set a header property to contain the 'claim' that can be used to retrieve the 'luggage'
    /// </summary>
    /// <param name="message">The message whose contents we want to </param>
    /// <param name="publication">The publication for the channel that the message is being published to; useful for metadata</param>
    /// <param name="cancellationToken">Add cancellation token</param>
    /// <returns>The message, with 'luggage' swapped out if over the threshold</returns>
    public async Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken = default)
    {
        if (System.Text.Encoding.Unicode.GetByteCount(message.Body.Value) < _thresholdInBytes)
        {
            return message;
        }

        var body = message.Body.Value;
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(body);
            
#if NETSTANDARD
            await writer.FlushAsync();
#else
        await writer.FlushAsync(cancellationToken);
#endif
        stream.Position = 0;

        var id = await _storeAsync.StoreAsync(stream, cancellationToken);

        message.Header.Bag[CLAIM_CHECK] = id;
        message.Header.DataRef = id;
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
        var id = message.Header.Bag.TryGetValue(CLAIM_CHECK, out object? objId) ? (string)objId : message.Header.DataRef; 
        if (string.IsNullOrEmpty(id))
        {
            return message;
        }
            
#if NETSTANDARD
            var luggage = await new StreamReader(await _storeAsync.RetrieveAsync(id!, cancellationToken))
                .ReadToEndAsync();
#else
        var luggage = await new StreamReader(await _storeAsync.RetrieveAsync(id, cancellationToken))
            .ReadToEndAsync(cancellationToken);
#endif
            
        var newBody = new MessageBody(luggage);
        message.Body = newBody;

        if (_retainLuggage)
        {
            return message;
        }
            
        _store.Delete(id!);
        message.Header.DataRef = null;

        return message;
    }

    /// <summary>
    /// If a message exceeds the threshold passed through the initializer list, then put it into storage
    /// If we place it in storage, set a header property to contain the 'claim' that can be used to retrieve the 'luggage'
    /// </summary>
    /// <param name="message">The message whose contents we want to </param>
    /// <param name="publication">The publication for the channel that the message is being published to; useful for metadata</param>
    /// <returns>The message, with 'luggage' swapped out if over the threshold</returns>
    public Message Wrap(Message message, Publication publication)
    {
        if (System.Text.Encoding.Unicode.GetByteCount(message.Body.Value) < _thresholdInBytes)
        {
            return message;
        }

        var body = message.Body.Value;
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(body);
        writer.Flush();
        stream.Position = 0;

        var id = _store.Store(stream);

        message.Header.Bag[CLAIM_CHECK] = id;
        message.Header.DataRef = id;
        message.Body = new MessageBody($"Claim Check {id}");

        return message;
    }

    /// <summary>
    /// If a message has a 'claim' in its header, then retrieve the associated 'luggage' from the store and replace the body
    /// </summary>
    /// <param name="message">The message, with luggage retrieved if required</param>
    /// <returns></returns>
    public Message Unwrap(Message message)
    {
        var id = message.Header.Bag.TryGetValue(CLAIM_CHECK, out object? objId) ? (string)objId : message.Header.DataRef; 
        if (string.IsNullOrEmpty(id))
        {
            return message;
        }
        
        var luggage = new StreamReader(_store.Retrieve(id!)).ReadToEnd();
        var newBody = new MessageBody(luggage);
        message.Body = newBody;

        if (_retainLuggage)
        {
            return message;
        }
            
        _store.Delete(id!);
        message.Header.DataRef = null;

        return message;
    }
}
