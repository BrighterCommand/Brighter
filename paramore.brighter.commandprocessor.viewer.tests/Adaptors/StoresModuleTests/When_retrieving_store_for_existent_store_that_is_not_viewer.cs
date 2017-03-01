using Nancy.Json;
using Nancy.Testing;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Modules;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors.StoresModuleTests
{
    [TestFixture]
    public class RetreiveMessageStoreNotInViewerTests
    {
        private Browser _browser;
        private BrowserResponse _result;
        private readonly string _storesUri = "/stores";

       [SetUp]
        public void Establish()
        {
            _browser = new Browser(new ConfigurableBootstrapper(with =>
            {
                ConfigureStoreModuleForStoreError(with, MessageStoreViewerModelError.StoreMessageViewerNotImplemented);
            }));
        }

        [Test]
        public void When_retrieving_store_for_existent_store_that_is_not_viewer()
        {
            _result = _browser.Get(_storesUri, with =>
                {
                    with.Header("accept", "application/json");
                    with.HttpRequest();
                })
                .Result;

            //should_return_404_NotFound
            _result.StatusCode.ShouldEqual(Nancy.HttpStatusCode.NotFound);
            //should_return_json
            _result.ContentType.ShouldContain("application/json");
            //should_return_error_detail
            var serializer = new JavaScriptSerializer();
            var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());

            model.ShouldNotBeNull();
            model.Message.ShouldContain("IMessageStoreViewer");

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