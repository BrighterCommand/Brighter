using Nancy.Json;
using Nancy.Testing;
using Xunit;
using Paramore.Brighter.MessageViewer.Adaptors.API.Modules;
using Paramore.Brighter.MessageViewer.Adaptors.API.Resources;
using Paramore.Brighter.MessageViewer.Ports.ViewModelRetrievers;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Adaptors.StoresModuleTests
{

    public class RetreiveMessageStoreReadFailureTests
    {
        private readonly string _storeUri = "/stores/storeName";
        private Browser _browser;
        private BrowserResponse _result;

        [SetUp]
        public void Establish()
        {
            _browser = new Browser(new ConfigurableBootstrapper(with =>
            {
                ConfigureStoreModuleForStoreError(with, MessageStoreViewerModelError.StoreMessageViewerGetException);
            }));
        }

        [Fact]
        public void When_retrieving_messages_with_store_that_cannot_get()
        {
            _result = _browser.Get(_storeUri, with =>
                {
                    with.Header("accept", "application/json");
                    with.HttpRequest();
                })
                .Result;

            //should_return_500_Server_error
            Assert.AreEqual(Nancy.HttpStatusCode.InternalServerError, _result.StatusCode);
            //should_return_json
            StringAssert.Contains("application/json", _result.ContentType);
            //should_return_error
            var serializer = new JavaScriptSerializer();
            var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());

            Assert.NotNull(model);
            StringAssert.Contains("Unable", model.Message);
        }

        private static void ConfigureStoreModuleForStoreError(
            ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator with,
            MessageStoreViewerModelError messageStoreViewerModelError)
        {
            var listViewRetriever = FakeActivationListModelRetriever.Empty();
            var storeRetriever = new FakeMessageStoreViewerModelRetriever(messageStoreViewerModelError);
            var messageRetriever = FakeMessageListViewModelRetriever.Empty();

            with.Module(new StoresNancyModule(listViewRetriever, storeRetriever, messageRetriever));
        }
    }
}