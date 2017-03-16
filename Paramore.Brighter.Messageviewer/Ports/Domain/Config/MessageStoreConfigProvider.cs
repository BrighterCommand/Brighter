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
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using Paramore.Brighter.MessageViewer.Adaptors.API.Configuration;

namespace Paramore.Brighter.MessageViewer.Ports.Domain.Config
{
    public class MessageStoreConfigProvider : IMessageStoreConfigProvider
    {
        private readonly MessageViewerConfiguration _config;
        private List<MessageStoreConfig> _stores;

        public MessageStoreConfigProvider(MessageViewerConfiguration config)
        {
            _config = config;
        }

        public IEnumerable<MessageStoreConfig> Get()
        {
            if (_stores == null)
            {
                LoadStores();
            }
            return _stores;
        }

        private void LoadStores()
        {
            _stores = new List<MessageStoreConfig>();
//            var configSection = MessageViewerConfiguration.Create();
            foreach (object store in _config.Stores)
            {
                var messageStore = store as MessageViewerConfigurationStore;
                if (messageStore != null)
                {
                    var messageStoreListItem = new MessageStoreConfig(messageStore);
                    _stores.Add(messageStoreListItem);
                }
            }
        }
    }
}

