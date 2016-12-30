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

using System;
using System.Collections.Generic;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using System.Linq;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain.Config;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers
{
    public interface IMessageStoreViewerModelRetriever
    {
        ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> Get(string storeName);
    }

    public class MessageStoreViewerModelRetriever : IMessageStoreViewerModelRetriever
    {
        private readonly IMessageStoreViewerFactory _storeViewerFactory;
        private readonly IMessageStoreConfigProvider _configProvider;

        public MessageStoreViewerModelRetriever(IMessageStoreViewerFactory storeViewerFactory,
            IMessageStoreConfigProvider configProvider)
        {
            _storeViewerFactory = storeViewerFactory;
            _configProvider = configProvider;
        }

        public ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> Get(string storeName)
        {
            try
            {
                IEnumerable<MessageStoreConfig> activationStates = _configProvider.Get();
                var foundState = activationStates.SingleOrDefault(msAs => msAs.Name == storeName);

                if (foundState != null)
                {
                    ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> errorResult;
                    var foundStore = GetStoreViewer(storeName, out errorResult);
                    if (foundStore == null) return errorResult;

                    var model = new MessageStoreViewerModel(foundStore, foundState);
                    return new ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError>(model);                    
                }
                return null;
            }
            catch (Exception e)
            {
                return new ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError>
                    (MessageStoreViewerModelError.GetActivationStateFromConfigError, e);
            }
        }

        private IAmAMessageStore<Message> GetStoreViewer(string storeName, out ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> errorResult)
        {
            IAmAMessageStore<Message> foundStore = _storeViewerFactory.Connect(storeName);
            if (foundStore == null)
            {
                {
                    errorResult = new ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError>(
                            MessageStoreViewerModelError.StoreNotFound);
                    return null;
                }
            }
            errorResult = null;
            return foundStore;
        }
    }

    public enum MessageStoreViewerModelError
    {
        GetActivationStateFromConfigError,
        StoreNotFound,
        StoreMessageViewerNotImplemented,
        StoreMessageViewerGetException
    }
}