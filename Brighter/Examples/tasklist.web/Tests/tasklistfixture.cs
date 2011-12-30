using System.IO;
using System.Reflection;
using Machine.Specifications;
using Nancy;
using Nancy.Testing;
using Simple.Data;

namespace tasklist.web.Tests
{
    [Subject("GET TaskList")]
    public class When_getting_the_current_task_list
    {
        static Browser _app;
        static BrowserResponse _response;
        static dynamic _db;
        static readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");


        Establish context = () =>
        {
            _db = Database.Opener.OpenFile(DatabasePath);
            _db.tasks.Insert(taskname: "confirm index list", taskdescription: "Ensure we display a list of tasks");
            var boostrapper = new DefaultNancyBootstrapper();
            _app = new Browser(boostrapper);
        }; 

        Because of = () => _response = _app.Get("/", with => with.HttpRequest());

        It should_return_success = () => ShouldExtensionMethods.ShouldEqual<HttpStatusCode>(_response.StatusCode, HttpStatusCode.OK);
        It shoud_contain_a_to_do_list = () => _response.Body["ul"].ShouldExistOnce();
    }
}
