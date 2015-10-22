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
using System.Linq;
using Machine.Specifications;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports
{
    [Subject(typeof(MessageStoreViewerModelRetriever))]
    public class MessageStoreViewerModelRetrieverTests
    {
        public class When_retrieving_json_for_valid_item
        {
            private Establish _context = () =>
            {
                _messageStore = MessageStoreConfigFactory.Create(_storeName, 
                    typeof(MsSqlMessageStore).FullName, _storeName, "table2");
                var fakeStoreListProvider = new FakeMessageStoreConfigProvider(_messageStore);

                var fakeStore = new FakeMessageStoreWithViewer();
                
                var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);
                _messageStoreViewerModelRetriever = new MessageStoreViewerModelRetriever(fakeMessageStoreFactory, fakeStoreListProvider);
            };
                
            private Because _of_GET = () => _result = _messageStoreViewerModelRetriever.Get(_storeName);

            private It should_return_model = () =>
            {
                var model = _result.Result;
                model.ShouldNotBeNull();
                model.Name.ShouldEqual(_messageStore.Name);
                model.ConnectionString.ShouldEqual(_messageStore.ConnectionString);
                model.TableName.ShouldEqual(_messageStore.TableName);
                model.TypeName.ShouldEqual(_messageStore.TypeName);
                model.Name.ShouldEqual(_messageStore.Name);
            };
            
            private static MessageStoreViewerModelRetriever _messageStoreViewerModelRetriever;
            private static ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> _result;
            private static string _storeName = "storeItemtestStoreName";
            private static MessageStoreConfig _messageStore;
        }

        public class When_retrieving_json_for_invalid_config 
        {
            private Establish _context = () =>
            {
                var fakeStoreListProvider = new FakeMessageStoreConfigProviderExceptionOnGet();
                var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(new FakeMessageStoreWithViewer(), _storeName);

                _messageStoreViewerModelRetriever = new MessageStoreViewerModelRetriever(fakeMessageStoreFactory, fakeStoreListProvider);
            };

            private Because _of_GET = () => _result = _messageStoreViewerModelRetriever.Get(_storeName);

            private It should_return_error = () =>
            {
                _result.IsError.ShouldBeTrue();
                _result.Error.ShouldEqual(MessageStoreViewerModelError.GetActivationStateFromConfigError);
                _result.Exception.ShouldNotBeNull();
            };

            private static MessageStoreViewerModelRetriever _messageStoreViewerModelRetriever;
            private static string _storeName = "invalidConfigStore";
            private static ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> _result;
        }

        public class When_retrieving_messages_for_non_existent_store
        {
            private Establish _context = () =>
            {
                var _messageStore = MessageStoreConfigFactory.Create(storeName,
                      typeof(MsSqlMessageStore).FullName, "sqlConfig", "table2");
                var fakeStoreListProvider = new FakeMessageStoreConfigProvider(_messageStore);
                var fakeMessageStoreFactory = FakeMessageStoreViewerFactory.CreateEmptyFactory();

                _messageStoreViewerModelRetriever = new MessageStoreViewerModelRetriever(fakeMessageStoreFactory, fakeStoreListProvider);
            };

            private static string storeName = "storeNamenotInStore";
            private Because _of_GET = () => _result = _messageStoreViewerModelRetriever.Get(storeName);

            private It should_return_error = () =>
            {
                var model = _result.Result;
                model.ShouldBeNull();
                _result.IsError.ShouldBeTrue();
                _result.Error.ShouldEqual(MessageStoreViewerModelError.StoreNotFound);
            };

            private static ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> _result;
            private static MessageStoreViewerModelRetriever _messageStoreViewerModelRetriever;
        }


        public class When_retrieving_messages_with_loader_that_errors
        {
            private static ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> _result;

            private Establish _context = () =>
            {
                var _messageStore = MessageStoreConfigFactory.Create(storeName,
                      typeof(MsSqlMessageStore).FullName, "sqlConfig", "table2");
                var fakeStoreListProvider = new FakeMessageStoreConfigProvider(_messageStore);
                IMessageStoreListCacheLoader fakeCacheLoaderThatErrors = new FakeMessageStoreListCacheLoaderThatErrors();
                var fakeMessageStoreFactory = new MessageStoreViewerFactory(fakeStoreListProvider, fakeCacheLoaderThatErrors);
                
                _messageStoreViewerModelRetriever = new MessageStoreViewerModelRetriever(fakeMessageStoreFactory, fakeStoreListProvider);
            };

            private static string storeName = "storeThatCannotGet";
            private Because _of_GET = () => _result = _messageStoreViewerModelRetriever.Get(storeName);

            private It should_return_error = () =>
            {
                var model = _result.Result;
                model.ShouldBeNull();
                _result.IsError.ShouldBeTrue();
                _result.Error.ShouldEqual(MessageStoreViewerModelError.GetActivationStateFromConfigError);
                _result.Exception.ShouldNotBeNull();
                _result.Exception.ShouldBeOfExactType<SystemException>();
            };

            private static MessageStoreViewerModelRetriever _messageStoreViewerModelRetriever;
        }
    }

    internal class FakeMessageStoreListCacheLoaderThatErrors : IMessageStoreListCacheLoader
    {
        public IMessageStoreConfigCache Load()
        {
            throw new SystemException();
        }
    }
}