﻿// ***********************************************************************
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
using Machine.Specifications;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports.MessageListViewModelRetrieverTests
{
    [Subject(typeof (MessageListViewModelRetriever))]
    public class MessageListViewModelRetrieverGetTests
    {
        public class When_retrieving_messages_for_a_store
        {
            private Establish _context = () =>
            {
                _messages = new List<Message>{
                    new Message(new MessageHeader(Guid.NewGuid(), "MyTopic1", MessageType.MT_COMMAND), new MessageBody("")), 
                    new Message(new MessageHeader(Guid.NewGuid(), "MyTopic2", MessageType.MT_COMMAND), new MessageBody(""))};

                var fakeStore = new FakeMessageStoreWithViewer();
                _messages.ForEach(m => fakeStore.Add(m));
                var modelFactory = new FakeMessageStoreViewerFactory(fakeStore, storeName);
                _messageListViewModelRetriever = new MessageListViewModelRetriever(modelFactory);
            };

            private static string storeName = "testStore";
            private Because _of_GET = () => _result = _messageListViewModelRetriever.Get(storeName);

            private It should_return_MessageListModel = () =>
            {
                var model = _result.Result;

                model.ShouldNotBeNull();
                model.MessageCount.ShouldEqual(_messages.Count);
            };

            private static MessageListViewModelRetriever _messageListViewModelRetriever;
            private static ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;
            private static List<Message> _messages;
        }

        public class When_retrieving_messages_for_non_existent_store
        {
            private Establish _context = () =>
            {
                var modelFactory = FakeMessageStoreViewerFactory.CreateEmptyFactory();
                _messageListViewModelRetriever = new MessageListViewModelRetriever(modelFactory);
            };

            private static string storeName = "storeNamenotInStore";
            private Because _of_GET = () => _result = _messageListViewModelRetriever.Get(storeName);

            private It should_return_error = () =>
            {
                var model = _result.Result;
                model.ShouldBeNull();
                _result.IsError.ShouldBeTrue();
                _result.Error.ShouldEqual(MessageListModelError.StoreNotFound);
            };

            private static IMessageStoreViewerFactory _viewerFactory;
            private static MessageListViewModelRetriever _messageListViewModelRetriever;
            private static ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;
        }

        public class When_retrieving_messages_for_existent_store_that_is_not_viewer
        {
            private Establish _context = () =>
            {
                var fakeStoreNotViewer = new FakeMessageStoreNotViewer();
                var modelFactory = new FakeMessageStoreViewerFactory(fakeStoreNotViewer, storeName);
                _messageListViewModelRetriever = new MessageListViewModelRetriever(modelFactory);
            };

            private static string storeName = "storeNotImplementingViewer";
            private Because _of_GET = () => _result = _messageListViewModelRetriever.Get(storeName);

            private It should_return_error = () =>
            {
                var model = _result.Result;
                model.ShouldBeNull();
                _result.IsError.ShouldBeTrue();
                _result.Error.ShouldEqual(MessageListModelError.StoreMessageViewerNotImplemented);
            };

            private static MessageListViewModelRetriever _messageListViewModelRetriever;
            private static ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;
            private static IMessageStoreViewerFactory _viewerFactory;
        }

        public class When_retrieving_messages_with_store_that_cannot_get
        {
            private static ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;

            private Establish _context = () =>
            {
                var fakeStoreNotViewer = new FakeMessageStoreViewerWithGetException();
                var modelFactory = new FakeMessageStoreViewerFactory(fakeStoreNotViewer, storeName);
                _messageListViewModelRetriever = new MessageListViewModelRetriever(modelFactory);
            };

            private static string storeName = "storeThatCannotGet";
            private Because _of_GET = () => _result = _messageListViewModelRetriever.Get(storeName);

            private It should_return_error = () =>
            {
                var model = _result.Result;
                model.ShouldBeNull();
                _result.IsError.ShouldBeTrue();
                _result.Error.ShouldEqual(MessageListModelError.StoreMessageViewerGetException);
                _result.Exception.ShouldNotBeNull();
                _result.Exception.ShouldBeOfExactType<AggregateException>();
            };

            private static IMessageStoreViewerFactory _viewerFactory;
            private static MessageListViewModelRetriever _messageListViewModelRetriever;
        }
    }
}
