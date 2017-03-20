using System.Collections.Generic;
using FluentAssertions;
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

    public class RetrieveMessageStoreContentTypeJsonTests
    {
        private readonly string _storeUri = "/stores/storeName";
        private Browser _browser;
        private BrowserResponse _result;

        public void Establish()
        {
            _browser = new Browser(new ConfigurableBootstrapper(with =>
            {
                var stores = new List<MessageStoreConfig>
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
            _result.StatusCode.Should().Be(Nancy.HttpStatusCode.OK);
            //should_return_json
            _result.ContentType.Should().Contain("application/json");
            //should_return_StoresListModel
            var serializer = new JavaScriptSerializer();
            var model = serializer.Deserialize<MessageStoreViewerModel>(_result.Body.AsString());

            model.Should().NotBeNull();
        }
  }
}