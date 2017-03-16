using System.Collections.Generic;
using System.Linq;
using Nancy;
using Nancy.Json;
using Nancy.Testing;
using NUnit.Framework;
using Paramore.Brighter.MessageStore.MsSql;
using Paramore.Brighter.MessageViewer.Adaptors.API.Modules;
using Paramore.Brighter.MessageViewer.Adaptors.API.Resources;
using Paramore.Brighter.MessageViewer.Ports.Domain.Config;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Adaptors.StoresModuleTests
{
     public class  RetrieveMessageStoresContentTypeJsonTests
     {
        private Browser _browser;
        private BrowserResponse _result;
        private string _storesUri = "/stores";

        [SetUp]
        public void Establish()
        {
            _browser = new Browser(new ConfigurableBootstrapper(with =>
            {
                var stores = new List<MessageStoreConfig>
                {
                    MessageStoreConfig.Create("store1", typeof (MsSqlMessageStore).FullName, "conn1", "table1"),
                    MessageStoreConfig.Create("store2", typeof (MsSqlMessageStore).FullName, "conn2", "table2")
                };
                var activationListModelRetriever = new FakeActivationListModelRetriever(new MessageStoreActivationStateListModel(stores));
                with.Module(new StoresNancyModule(activationListModelRetriever, new FakeMessageStoreViewerModelRetriever(), new FakeMessageListViewModelRetriever()));
            }));
        }

        [Test]
        public void When_retrieving_stores_json()
        {
            _result = _browser.Get(_storesUri, with =>
            {
                with.Header("accept", "application/json");
                     with.HttpRequest();
            })
            .Result;

             //should_return_200_OK
            Assert.AreEqual(HttpStatusCode.OK, _result.StatusCode);
            //should_return_json
            StringAssert.Contains("application/json", _result.ContentType);
            //should_return_StoresListModel
             var serializer = new JavaScriptSerializer();
             var model = serializer.Deserialize<MessageStoreActivationStateListModel>(_result.Body.AsString());

            Assert.NotNull(model);
            Assert.AreEqual(2, model.stores.Count());
        }
   }
}