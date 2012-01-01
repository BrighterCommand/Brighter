using System.IO;
using System.Reflection;
using Machine.Specifications;
using Nancy;
using Nancy.Testing;
using Simple.Data;

namespace tasklist.web.Tests
{
    [Subject("GET: Task List")]
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

        Because of = () => _response = _app.Get("/todo/index", with => with.HttpRequest());

        It should_have_a_list = () => _response.Body["ul"].ShouldExistOnce();
        It should_have_a_task_name_input_field_on_the_page = () => _response.Body["input#taskName"].ShouldExistOnce();
        It should_have_a_task_description_field_on_the_page = () => _response.Body["input#taskDescription"].ShouldExistOnce();
        It should_have_a_button_to_add_the_task = () => _response.Body["input#addTask"].ShouldExistOnce();
    }
}
