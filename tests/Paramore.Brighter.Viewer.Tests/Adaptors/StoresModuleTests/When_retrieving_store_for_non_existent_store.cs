using FluentAssertions;
using Nancy.Json;
using Nancy.Testing;
using Xunit;
using Paramore.Brighter.MessageViewer.Adaptors.API.Modules;
using Paramore.Brighter.MessageViewer.Adaptors.API.Resources;
using Paramore.Brighter.MessageViewer.Ports.ViewModelRetrievers;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Adaptors.StoresModuleTests
{
    public class RetrieveMessagesStoreNonExistantStoreTests
    {
        private static string _storeUri = "/stores/storeName";
        private Browser _browser;
        private BrowserResponse _result;

        public RetrieveMessagesStoreNonExistantStoreTests()
        {
            _browser = new Browser(new ConfigurableBootstrapper(with =>
            {
                ConfigureStoreModuleForStoreError(with, MessageStoreViewerModelError.StoreNotFound);
            }));
        }

        [Fact]
        public void When_retrieving_store_for_non_existent_store()
        {
            _result = _browser.Get(_storeUri, with =>
                {
                    with.Header("accept", "application/json");
                    with.HttpRequest();
                })
                .Result;

            //should_return_404_NotFound
            _result.StatusCode.Should().Be(Nancy.HttpStatusCode.NotFound);
            //should_return_json
            _result.ContentType.Should().Contain("application/json");
            //should_return_error_detail
            var serializer = new JavaScriptSerializer();
            var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());
            model.Should().NotBeNull();
            model.Message.Should().Contain("Unknown");
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