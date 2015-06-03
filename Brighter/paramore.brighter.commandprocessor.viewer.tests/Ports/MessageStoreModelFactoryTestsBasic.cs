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
using paramore.brighter.commandprocessor.messagestore.ravendb;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports
{
    [Subject(typeof(MessageStoreViewerFactory))]
    public class MessageStoreModelFactoryTestsBasic
    {
        public abstract class when_creating_a_message_store_Base
        {
            //TODO: as statics can't use abstract, I'd perfer something better!
            protected when_creating_a_message_store_Base(MessageStoreActivationState item)
            {
                MessageStoreActivationState = item;
            }
            private Establish _context = () =>
            {
                _provider = new FakeMessageStoreActivationStateProvider(MessageStoreActivationState);
            };

            private Because _of = () => { _factory = new MessageStoreViewerFactory(_provider, new MessageStoreListCacheLoader(new MessageStoreActivationStateCache())); };

            private static MessageStoreViewerFactory _factory;
            private static FakeMessageStoreActivationStateProvider _provider;
            protected static MessageStoreActivationState MessageStoreActivationState;

            protected static void AssertStoreFromFactory()
            {
                try
                {
                    IAmAMessageStore<Message> messageStore = _factory.Connect(MessageStoreActivationState.Name);
                    messageStore.GetType().FullName.ShouldEqual(MessageStoreActivationState.TypeName);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw e;
                }
            }
        }

        public class when_creating_a_raven_remote_message_store : when_creating_a_message_store_Base
        {
            private It should_create_expected_store = () => AssertStoreFromFactory();

            public when_creating_a_raven_remote_message_store() 
                : base(MessageStoreActivationStateFactory.Create("remoteRaven", typeof(RavenMessageStore).FullName,
                    @"Url = http://ravendb.mydomain.com;User=user;Password=secret"))
            {
            }
        }

        public class when_creating_a_raven_local__message_store : when_creating_a_message_store_Base
        {
            private It should_create_expected_store = () => AssertStoreFromFactory();

            public when_creating_a_raven_local__message_store()
                : base(MessageStoreActivationStateFactory.Create("localRaven", typeof(RavenMessageStore).FullName, 
                    @"Url = DataDir = ~\App_Data\RavenDB;Enlist=False"))
                    //@"Url = DataDir = ."))
            {
            }
        }
        public class when_creating_a_sql_2008_message_store : when_creating_a_message_store_Base
        {
            private It should_create_expected_store = () => AssertStoreFromFactory();

            public when_creating_a_sql_2008_message_store()
                : base(MessageStoreActivationStateFactory.Create("sql2008", typeof(MsSqlMessageStore).FullName, 
                    "Server=.;Database=aMessageStore;Trusted_Connection=True", "table1"))
            {
            }
        }
        public class when_creating_a_sql_ce_message_store : when_creating_a_message_store_Base
        {
            private It should_create_expected_store = () => AssertStoreFromFactory();

            public when_creating_a_sql_ce_message_store()
                : base(MessageStoreActivationStateFactory.Create("sqlce", typeof(MsSqlMessageStore).FullName,
                    "DataSource='test.sdf';", "table2"))
            {
            }
        }
    }
}