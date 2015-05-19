// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 25-03-2014
//
// Last Modified By : ian
// Last Modified On : 25-03-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion
using System;
using System.Collections.Generic;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.Domain
{
    public interface IMessageStoreActivationState
    {
        IAmAMessageStore<Message> Get(MessageStoreActivationState type);
        void Set(MessageStoreType storeType, Func<MessageStoreActivationState, IAmAMessageStore<Message>> storeCtor);
    }

    public class MessageStoreActivationStateCache : IMessageStoreActivationState
    {
        private readonly Dictionary<MessageStoreType, Func<MessageStoreActivationState, IAmAMessageStore<Message>>> _storeCtorLookup = new Dictionary<MessageStoreType, Func<MessageStoreActivationState, IAmAMessageStore<Message>>>();
        private readonly Dictionary<MessageStoreType, IAmAMessageStore<Message>> _storesCreated = new Dictionary<MessageStoreType, IAmAMessageStore<Message>>();

        public IAmAMessageStore<Message> Get(MessageStoreActivationState messageStore)
        {
            if (!_storesCreated.ContainsKey(messageStore.StoreType))
            {
                _storesCreated.Add(messageStore.StoreType, _storeCtorLookup[messageStore.StoreType].Invoke(messageStore));
            }
            return _storesCreated[messageStore.StoreType];
        }

        public void Set(MessageStoreType storeType, Func<MessageStoreActivationState, IAmAMessageStore<Message>> storeCtor)
        {
            _storeCtorLookup.Add(storeType, storeCtor);
        }
    }
}