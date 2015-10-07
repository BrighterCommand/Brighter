// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ianp
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

using System.Collections.Generic;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeMessageStoreListCacheLoader : IMessageStoreListCacheLoader
    {
        private readonly IMessageStoreConfigCache _messageStoreConfigCache;
        public Dictionary<MessageStoreType, int> ctorCalled = new Dictionary<MessageStoreType, int>();

        public FakeMessageStoreListCacheLoader(IMessageStoreConfigCache messageStoreConfigCache)
        {
            _messageStoreConfigCache = messageStoreConfigCache;
        }

        public IMessageStoreConfigCache Load()
        {
            return _messageStoreConfigCache;
        }

        public void Setup(MessageStoreType type, FakeMessageStoreWithViewer fakeMessageStoreWithViewer)
        {
            _messageStoreConfigCache.Set(type, msli =>
            {
                if (!ctorCalled.ContainsKey(type))
                {
                    ctorCalled.Add(type,0);
                }
                ctorCalled[type]++;
                return fakeMessageStoreWithViewer;
            });
        }
    }
}