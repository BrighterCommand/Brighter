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
using Machine.Specifications;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports
{
    [Subject(typeof(MessageStoreViewerFactory))]
    public class MessageStoreModelFactoryTestsComplex
    {
        public class when_creating_an_unknown_message_store_connection_string
        {
            //TODO: as statics can't use abstract, I'd perfer something better!

            private Because _of = () => exception = Catch.Exception(() =>
            {
                MessageStoreActivationState = MessageStoreActivationStateFactory.Create("sqlce", typeof(Array).FullName,
                    "notValid", "table2");
                _provider = new FakeMessageStoreActivationStateProvider(MessageStoreActivationState);
            });

            private It should_throw_argument_exception = () => exception.ShouldBeOfExactType<ArgumentException>();
            private static FakeMessageStoreActivationStateProvider _provider;
            protected static MessageStoreActivationState MessageStoreActivationState;
            private static Exception exception;
        }

        public class when_creating_a_store_with_same_type_twice
        {
            private Establish _context = () =>
            {
                storeName = "sqlce-1";
                _provider = FakeMessageStoreActivationStateProvider.CreateEmpty();
                _provider.Add(MessageStoreActivationStateFactory.Create(storeName, typeof(MsSqlMessageStore).FullName,
                    "DataSource='test-1.sdf';", "table-1"));
                _provider.Add(MessageStoreActivationStateFactory.Create("sqlce-2", typeof(MsSqlMessageStore).FullName,
                    "DataSource='test-2.sdf';", "table-2"));
                var messageStoreListCache = new MessageStoreActivationStateCache();
                _fakeMessageStoreListCacheLoader = new FakeMessageStoreListCacheLoader(messageStoreListCache);
                _fakeMessageStoreListCacheLoader.Setup(MessageStoreType.SqlCe, new FakeMessageStoreWithViewer());
                _factory = new MessageStoreViewerFactory(_provider, _fakeMessageStoreListCacheLoader);                
            };

            private Because _of = () => exception = Catch.Exception(() =>
            {
                _factory.Connect(storeName);
            });

            private It should_throw_argument_exception = () => exception.ShouldBeNull();
            private static FakeMessageStoreActivationStateProvider _provider;
            protected static MessageStoreActivationState MessageStoreActivationState;
            private static Exception exception;
            private static FakeMessageStoreListCacheLoader _fakeMessageStoreListCacheLoader;
            private static MessageStoreViewerFactory _factory;
            private static string storeName;
        }


        public class when_creating_a_sql_ce_message_store_twice
        {
            private Establish _context = () =>
            {
                storeName = "sqlce";
                var messageStoreListItem = MessageStoreActivationStateFactory.Create(storeName, typeof(MsSqlMessageStore).FullName,
                    "DataSource='test.sdf';", "table2");
                _provider = new FakeMessageStoreActivationStateProvider(messageStoreListItem);

                var messageStoreListCache = new MessageStoreActivationStateCache();
                _fakeMessageStoreListCacheLoader = new FakeMessageStoreListCacheLoader(messageStoreListCache);
                _fakeMessageStoreListCacheLoader.Setup(MessageStoreType.SqlCe, new FakeMessageStoreWithViewer());
                _factory = new MessageStoreViewerFactory(_provider, _fakeMessageStoreListCacheLoader);
                _factory.Connect(storeName);
            };

            private Because _of = () => _factory.Connect(storeName);
            private It should_call_from_source_list_once = () => 
                _fakeMessageStoreListCacheLoader.ctorCalled[MessageStoreType.SqlCe].ShouldEqual(1);

            private static MessageStoreViewerFactory _factory;
            private static FakeMessageStoreActivationStateProvider _provider;
            private static string storeName;
            private static FakeMessageStoreListCacheLoader _fakeMessageStoreListCacheLoader;
        }
    }
}
