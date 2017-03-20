using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Nancy;
using Nancy.Json;
using Nancy.Testing;
using Xunit;
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

        public RetrieveMessageStoresContentTypeJsonTests()
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

        [Fact]
        public void When_retrieving_stores_json()
        {
            _result = _browser.Get(_storesUri, with =>
            {
                with.Header("accept", "application/json");
                     with.HttpRequest();
            })
            .Result;

             //should_return_200_OK
            _result.StatusCode.Should().Be(HttpStatusCode.OK);
            //should_return_json
            _result.ContentType.Should().Contain("application/json");
            //should_return_StoresListModel
             var serializer = new JavaScriptSerializer();
             var model = serializer.Deserialize<MessageStoreActivationStateListModel>(_result.Body.AsString());

            model.Should().NotBeNull();
            model.stores.Count().Should().Be(2);
        }
   }
}