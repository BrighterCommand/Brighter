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
using Nancy;
using Nancy.Json;
using Nancy.Testing;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Modules;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;
using NUnit.Specifications;
using nUnitShouldAdapter;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain.Config;
using paramore.brighter.commandprocessor.messagestore.mssql;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors.StoresModuleTests
{
    [Subject(typeof(StoresNancyModule))]
    public class StoresModuleItemTests
    {
        private static string _storeUri = "/stores/storeName";

        [Subject(typeof(StoresNancyModule))]
        public class When_retrieving_store_json : NUnit.Specifications.ContextSpecification
        {
            private Establish _context = () =>
            {
                _browser = new Browser(new ConfigurableBootstrapper(with =>
                {
                    var stores = new List<paramore.brighter.commandprocessor.messageviewer.Ports.Domain.Config.MessageStoreConfig>
                    {
                        MessageStoreConfig.Create("store1", typeof (MsSqlMessageStore).FullName, "conn1", "table1"),
                        MessageStoreConfig.Create("store2", typeof (MsSqlMessageStore).FullName, "conn2", "table2")
                    };
                    var messageStoreActivationStateListViewModelRetriever = new FakeActivationListModelRetriever(new MessageStoreActivationStateListModel(stores));
                    var viewrModelRetriever = new FakeMessageStoreViewerModelRetriever(new MessageStoreViewerModel());
                    var retriever = new FakeMessageListViewModelRetriever();

                    with.Module(new StoresNancyModule(messageStoreActivationStateListViewModelRetriever, viewrModelRetriever, retriever));
                }));
            };

            private Because _of_GET = () => _result = _browser.Get(_storeUri, with =>
            {
                with.Header("accept", "application/json");
                with.HttpRequest();
            }).Result;

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
        }

        public class When_retrieving_store_for_non_existent_store : NUnit.Specifications.ContextSpecification
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
            }).Result;

            private It should_return_404_NotFound = () => _result.StatusCode.ShouldEqual(Nancy.HttpStatusCode.NotFound);
            private It should_return_json = () => _result.ContentType.ShouldContain("application/json");

            private It should_return_error_detail = () =>
            {
                var serializer = new JavaScriptSerializer();
                var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());
                model.ShouldNotBeNull();
                model.Message.ShouldContain("Unknown");
            };

            private static Browser _browser;
            protected static BrowserResponse _result;
        }

        public class When_retrieving_store_for_existent_store_that_is_not_viewer : NUnit.Specifications.ContextSpecification
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
            }).Result;


            private It should_return_404_NotFound = () => _result.StatusCode.ShouldEqual(Nancy.HttpStatusCode.NotFound);
            private It should_return_json = () => _result.ContentType.ShouldContain("application/json");

            private It should_return_error_detail = () =>
            {
                var serializer = new JavaScriptSerializer();
                var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());

                model.ShouldNotBeNull();
                model.Message.ShouldContain("IMessageStoreViewer");
            };

            private static Browser _browser;
            protected static BrowserResponse _result;
        }

        public class When_retrieving_messages_with_store_that_cannot_get : NUnit.Specifications.ContextSpecification
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
            }).Result;

            private It should_return_500_Server_error = () => _result.StatusCode.ShouldEqual(Nancy.HttpStatusCode.InternalServerError);
            private It should_return_json = () => _result.ContentType.ShouldContain("application/json");
            private It should_return_error = () =>
            {
                var serializer = new JavaScriptSerializer();
                var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());

                model.ShouldNotBeNull();
                model.Message.ShouldContain("Unable");
            };

            private static Browser _browser;
            protected static BrowserResponse _result;
        }


        public class When_retrieving_messages_with_store_when_config_unavailable : NUnit.Specifications.ContextSpecification
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
            }).Result;

            private It should_return_500_Server_error = () => _result.StatusCode.ShouldEqual(Nancy.HttpStatusCode.InternalServerError);
            private It should_return_json = () => _result.ContentType.ShouldContain("application/json");
            private It should_return_error = () =>
            {
                var serializer = new JavaScriptSerializer();
                var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());

                model.ShouldNotBeNull();
                model.Message.ShouldContain("Mis-configured");
            };

            private static Browser _browser;
            protected static BrowserResponse _result;
        }

        private static void ConfigureStoreModuleForStoreError(ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator with, MessageStoreViewerModelError messageStoreViewerModelError)
        {
            var listViewRetriever = FakeActivationListModelRetriever.Empty();
            var storeRetriever = new FakeMessageStoreViewerModelRetriever(messageStoreViewerModelError);
            var messageRetriever = FakeMessageListViewModelRetriever.Empty();

            with.Module(new StoresNancyModule(listViewRetriever, storeRetriever, messageRetriever));
        }
    }
}