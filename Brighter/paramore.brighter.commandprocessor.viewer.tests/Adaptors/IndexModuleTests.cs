using Machine.Specifications;
using Nancy;
using Nancy.Testing;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Handlers;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors
{
    [Subject(typeof(IndexModule))]
    public class When_retrieving_home
    {
        private Establish _context = () =>
        {
            browser = new Browser(new ConfigurableBootstrapper(with => with.Module<IndexModule>()));
        };

        private Because _with_GET = () => result = browser.Get("/", with =>
        {
            with.HttpRequest();
            with.Header("accept", "text/html");
        });

        private It should_return_200_OK = () => result.StatusCode.ShouldEqual(HttpStatusCode.OK);
        private It should_return_text_html = () => result.ContentType.ShouldEqual("text/html");

        private static Browser browser;
        private static BrowserResponse result;
    }
}