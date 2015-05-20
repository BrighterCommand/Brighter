using Machine.Specifications;
using Nancy.Json;
using Nancy.Testing;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;

namespace paramore.brighter.commandprocessor.viewer.tests.TestBehaviours
{
    [Behaviors]
    public class ModuleWithNoStoreConnectionBehavior<T>
    {
        protected static BrowserResponse _result;
        private It should_return_404_NotFound = () => _result.StatusCode.ShouldEqual(Nancy.HttpStatusCode.NotFound);
        private It should_return_json = () => _result.ContentType.ShouldContain("application/json");

        private It should_return_error_detail = () =>
        {
            var serializer = new JavaScriptSerializer();
            var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());
            model.ShouldNotBeNull();
            model.Message.ShouldContain("Unknown");
            //            model.Message.ShouldContain(storeName);

        };
    }
}