using Machine.Specifications;
using Nancy.Json;
using Nancy.Testing;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;

namespace paramore.brighter.commandprocessor.viewer.tests.TestBehaviours
{
    [Behaviors]
    public class ModuleWithStoreCantGetBehaviour
    {
        protected static BrowserResponse _result;

        private It should_return_500_Server_error =
            () => _result.StatusCode.ShouldEqual(Nancy.HttpStatusCode.InternalServerError);

        private It should_return_json = () => _result.ContentType.ShouldContain("application/json");

        private It should_return_error = () =>
        {
            var serializer = new JavaScriptSerializer();
            var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());

            model.ShouldNotBeNull();
            model.Message.ShouldContain("Unable");
            //model.Message.ShouldContain(storeName);
        };
    }
}