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
using Machine.Specifications;
using Nancy;
using Nancy.Json;
using Nancy.Testing;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Handlers;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestBehaviours;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors.StoresModuleTests2
{
    [Subject(typeof(StoresNancyModule))]
    public class StoresModuleItemTests
    {
        private static string _storeUri = "/stores/storeName";

        [Subject(typeof(StoresNancyModule))]
        public class When_retrieving_store_json
        {
            private Establish _context = () =>
            {
                _browser = new Browser(new ConfigurableBootstrapper(with =>
                {
                    var stores = new List<MessageStoreActivationState>
                    {
                        MessageStoreActivationStateFactory.Create("store1", typeof (MsSqlMessageStore).FullName, "conn1", "table1"),
                        MessageStoreActivationStateFactory.Create("store1", typeof (MsSqlMessageStore).FullName, "conn1","table1")
                    };
                    with.StoresModule(stores);
                }));
            };

            private Because _of_GET = () => _result = _browser.Get(_storeUri, with =>
            {
                with.Header("accept", "application/json");
                with.HttpRequest();
            });

            private It should_return_200_OK = () => _result.StatusCode.ShouldEqual(HttpStatusCode.OK);
            private It should_return_json = () => _result.ContentType.ShouldContain("application/json");
            private It should_return_StoresListModel = () =>
            {
                var serializer = new JavaScriptSerializer();
                var model = serializer.Deserialize<MessageStoreViewerModel>(_result.Body.AsString());

                model.ShouldNotBeNull();
            };

            private static Browser _browser;
            protected static BrowserResponse _result;
            private static List<Message> _messages;

        }

        public class When_retrieving_store_for_non_existent_store
        {
            Establish _context = () =>
            {
                _browser = new Browser(new ConfigurableBootstrapper(with =>
                {
                    ConfigureStoreModuleForStoreError(with, MessageStoreViewerModelError.StoreNotFound);
                }));
            };

            Because _of_GET = () => _result = _browser.Get(_storeUri, with =>
            {
                with.Header("accept", "application/json");
                with.HttpRequest();
            });

            private Behaves_like<ModuleWithNoStoreConnectionBehavior<MessageViewerError>> noStore;

            private static Browser _browser;
            protected static BrowserResponse _result;
        }
        
        public class When_retrieving_store_for_existent_store_that_is_not_viewer
        {
            private Establish _context = () =>
            {
                _browser = new Browser(new ConfigurableBootstrapper(with =>
                {
                    ConfigureStoreModuleForStoreError(with, MessageStoreViewerModelError.StoreMessageViewerNotImplemented);
                }));
            };

            Because _of_GET = () => _result = _browser.Get(_storeUri, with =>
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
                    ConfigureStoreModuleForStoreError(with, MessageStoreViewerModelError.StoreMessageViewerGetException);
                }));
            };

            Because _of_GET = () => _result = _browser.Get(_storeUri, with =>
            {
                with.Header("accept", "application/json");
                with.HttpRequest();
            });

            Behaves_like<ModuleWithStoreCantGetBehaviour> storeCantGet;

            private static Browser _browser;
            protected static BrowserResponse _result;
        }


        public class When_retrieving_messages_with_store_when_config_unavailable
        {
            private Establish _context = () =>
            {
                _browser = new Browser(new ConfigurableBootstrapper(with =>
                {
                    ConfigureStoreModuleForStoreError(with, MessageStoreViewerModelError.GetActivationStateFromConfigError);
                }));
            };

            Because _of_GET = () => _result = _browser.Get(_storeUri, with =>
            {
                with.Header("accept", "application/json");
                with.HttpRequest();
            });

            Behaves_like<ModuleWithBadConfigBehavior> storeHasBadConfig;

            private static Browser _browser;
            protected static BrowserResponse _result;
        }

        private static void ConfigureStoreModuleForStoreError(ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator with, MessageStoreViewerModelError messageStoreViewerModelError)
        {
            var listViewRetriever = FakeActivationListModelRetriever.Empty();
            var storeRetriever = new FakeMessageStoreViewerModelRetriever(messageStoreViewerModelError);
            var messageRetriever = FakeMessageListViewModelRetriever.Empty();

            with.StoresModule(listViewRetriever, storeRetriever, messageRetriever);
        }
    }
}