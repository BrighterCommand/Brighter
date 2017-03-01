using System.Collections.Generic;
using System.Linq;
using Nancy;
using Nancy.Json;
using Nancy.Testing;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Modules;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain.Config;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors.StoresModuleTests
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
                var stores = new List<paramore.brighter.commandprocessor.messageviewer.Ports.Domain.Config.MessageStoreConfig>
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
             _result.StatusCode.ShouldEqual(HttpStatusCode.OK);
             //should_return_json
             _result.ContentType.ShouldContain("application/json");
             //should_return_StoresListModel
             var serializer = new JavaScriptSerializer();
             var model = serializer.Deserialize<MessageStoreActivationStateListModel>(_result.Body.AsString());

             model.ShouldNotBeNull();
             model.stores.Count().ShouldEqual(2);
         }
   }
}