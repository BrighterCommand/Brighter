using System.Collections.Generic;
using System.Net;
using Nancy.Json;
using Nancy.Testing;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Modules;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain.Config;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;
using Paramore.Brighter.Messagestore.MsSql;
using HttpStatusCode = Nancy.HttpStatusCode;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors.StoresModuleTests
{
    [TestFixture]
    public class RetrieveMessageStoreContentTypeJsonTests
    {
        private readonly string _storeUri = "/stores/storeName";
        private Browser _browser;
        private BrowserResponse _result;

        public void Establish()
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
        }

        public void When_retrieving_store_json()
        {
            _result = _browser.Get(_storeUri, with =>
                {
                    with.Header("accept", "application/json");
                    with.HttpRequest();
                })
                .Result;

            //should_return_200_OK
            Assert.AreEqual(Nancy.HttpStatusCode.OK, _result.StatusCode);
            //should_return_json
            StringAssert.Contains("application/json", _result.ContentType);
            //should_return_StoresListModel
            var serializer = new JavaScriptSerializer();
            var model = serializer.Deserialize<MessageStoreViewerModel>(_result.Body.AsString());

            Assert.NotNull(model);
        }
  }
}