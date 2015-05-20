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
using System.Linq;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers
{
    public enum MessageListModelError
    {
        StoreNotFound,
        StoreMessageViewerNotImplemented,
        StoreMessageViewerGetException,
        GetActivationStateFromConfigError
    }
    
    public interface IMessageListViewModelRetriever
    {
        ViewModelRetrieverResult<MessageListModel, MessageListModelError> Get(string storeName);
        ViewModelRetrieverResult<MessageListModel, MessageListModelError> Filter(string storeName, string searchTerm);
    }

    public class MessageListViewModelRetriever : IMessageListViewModelRetriever
    {
        private readonly IMessageStoreViewerFactory _messageStoreViewerFactory;

        public MessageListViewModelRetriever(IMessageStoreViewerFactory messageStoreViewerFactory)
        {
            _messageStoreViewerFactory = messageStoreViewerFactory;
        }

        public ViewModelRetrieverResult<MessageListModel, MessageListModelError> Get(string storeName)
        {
            ViewModelRetrieverResult<MessageListModel, MessageListModelError> errorResult;
            IAmAMessageStoreViewer<Message> foundViewer = GetStoreViewer(storeName, out errorResult);
            if (foundViewer == null) return errorResult;
            try
            {
                var messages = foundViewer.Get().Result;
                var messageListModel = new MessageListModel(messages);

                return new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(messageListModel);
            }
            catch (Exception e)
            {
                return new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(MessageListModelError.StoreMessageViewerGetException, e);
            }
        }

        public ViewModelRetrieverResult<MessageListModel, MessageListModelError> Filter(string storeName, string searchTerm)
        {
            ViewModelRetrieverResult<MessageListModel, MessageListModelError> errorResult;
            IAmAMessageStoreViewer<Message> foundViewer = GetStoreViewer(storeName, out errorResult);
            if (foundViewer == null) return errorResult;

            //in-memory VERY cheap search
            int pageSize = 5000;
            int pageNumber = 1;

            var foundMessages = new List<Message>();
            try
            {
                IList<Message> messages;
                do
                {
                    messages = foundViewer.Get(pageSize, pageNumber).Result;
                    foundMessages.AddRange(messages.Where(m => m.Body.Value.Contains(searchTerm)
                                                               || m.Header.Topic.Contains(searchTerm)
                                                               || m.Header.Bag.Keys.Any(k => k.Contains(searchTerm))
                                                               || m.Header.Bag.Values.Any(v => v.ToString().Contains(searchTerm))
                                                               || m.Header.TimeStamp.ToString().Contains(searchTerm)));

                    pageNumber++;
                } while (messages.Count == pageSize);

                return new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(new MessageListModel(foundMessages));
            }
            catch (Exception e)
            {
                return new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(MessageListModelError.StoreMessageViewerGetException, e);
            }
        }

        private IAmAMessageStoreViewer<Message> GetStoreViewer(string storeName, out ViewModelRetrieverResult<MessageListModel, MessageListModelError> errorResult)
        {
            IAmAMessageStore<Message> foundStore = _messageStoreViewerFactory.Connect(storeName);
            if (foundStore == null)
            {
                errorResult = new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(MessageListModelError.StoreNotFound);
                return null;
            }
            var foundViewer = foundStore as IAmAMessageStoreViewer<Message>;
            if (foundViewer == null)
            {
                errorResult = new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(MessageListModelError.StoreMessageViewerNotImplemented);
                return null;
            }
            errorResult = null;
            return foundViewer;
        }
    }
}