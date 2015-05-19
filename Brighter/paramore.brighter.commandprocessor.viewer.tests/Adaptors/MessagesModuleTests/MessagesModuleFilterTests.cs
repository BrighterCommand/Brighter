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
using Nancy.Json;
using Nancy.Testing;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Handlers;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestBehaviours;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors.MessagesModuleTests
{
    [Subject(typeof (MessagesNancyModule))]
    public class MessagesModuleFilterTests
    {
        public class When_retrieving_messages_for_a_store
        {
            private Establish _context = () =>
            {
                var messages = new List<Message>
                {
                    new Message(new MessageHeader(Guid.NewGuid(), "MyTopic1", MessageType.MT_COMMAND),
                        new MessageBody("")),
                    new Message(new MessageHeader(Guid.NewGuid(), "MyTopic2", MessageType.MT_COMMAND),
                        new MessageBody(""))
                };

                _browser = new Browser(new ConfigurableBootstrapper(with =>
                {
                    var messageListViewModelRetriever = new FakeMessageListViewModelRetriever(new MessageListModel(messages));
                    with.MessagesModule(messageListViewModelRetriever);
                }));
            };

            private static string storeName = "testStore";
            private static string uri = string.Format("/messages/{0}", storeName);

            private Because _of_GET = () => _result = _browser.Get(uri, with =>
            {
                with.Header("accept", "application/json");
                with.HttpRequest();
            });

            private It should_return_200_OK = () => _result.StatusCode.ShouldEqual(Nancy.HttpStatusCode.OK);
            private It should_return_json = () => _result.ContentType.ShouldContain("application/json");
            private It should_return_MessageListModel = () =>
            {
                var serializer = new JavaScriptSerializer();
                var model = serializer.Deserialize<MessageListModel>(_result.Body.AsString());
                model.ShouldNotBeNull();
            };

            private static Browser _browser;
            private static BrowserResponse _result;
        }

        public class When_retrieving_messages_for_non_existent_store
        {
            Establish _context = () =>
            {
                _browser = new Browser(new ConfigurableBootstrapper(with =>
                {
                    var messageListViewModelRetriever = new FakeMessageListViewModelRetriever(MessageListModelError.StoreNotFound);
                    with.MessagesModule(messageListViewModelRetriever);
                }));
            };

            private static string storeName = "storeNamenotInStore";
            private static string uri = string.Format("/messages/{0}", storeName);

            Because _of_GET = () => _result = _browser.Get(uri, with =>
            {
                with.Header("accept", "application/json");
                with.HttpRequest();
            });

            private Behaves_like<ModuleWithNoStoreConnectionBehavior<MessageViewerError>> noStore;

            private static Browser _browser;
            protected static BrowserResponse _result;
        }

        public class When_retrieving_messages_for_existent_store_that_is_not_viewer
        {
            private Establish _context = () =>
            {
                _browser = new Browser(new ConfigurableBootstrapper(with =>
                {
                    var messageListViewModelRetriever =
                        new FakeMessageListViewModelRetriever(MessageListModelError.StoreMessageViewerNotImplemented);
                    with.MessagesModule(messageListViewModelRetriever);
                }));
            };

            private static string storeName = "storeNotImplementingViewer";
            private static string uri = string.Format("/messages/{0}", storeName);

            private Because _of_GET = () => _result = _browser.Get(uri, with =>
            {
                with.Header("accept", "application/json");
                with.HttpRequest();
            });

            private Behaves_like<ModuleWithStoreNotViewerBehavior> storeWithoutViewer; 

            private static Browser _browser;
            protected static BrowserResponse _result;
        }

        public class When_retrieving_messages_with_store_that_cannot_get
        {
            private Establish _context = () =>
            {
                _browser = new Browser(new ConfigurableBootstrapper(with =>
                {
                    var messageListViewModelRetriever =
                        new FakeMessageListViewModelRetriever(MessageListModelError.StoreMessageViewerGetException);
                    with.MessagesModule(messageListViewModelRetriever);
                }));
            };

            private static string storeName = "storeThatCannotGet";
            private static string uri = string.Format("/messages/{0}", storeName);

            private Because _of_GET = () => _result = _browser.Get(uri, with =>
            {
                with.Header("accept", "application/json");
                with.HttpRequest();
            });

            Behaves_like<ModuleWithStoreCantGetBehaviour> storeCantGet; 

            private static Browser _browser;
            protected static BrowserResponse _result;
        }
    }
}
