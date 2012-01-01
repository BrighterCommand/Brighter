using System.IO;
using System.Reflection;
using NUnit.Framework;
using Nancy;
using Nancy.Testing;
using Simple.Data;

namespace tasklist.web.Tests
{
    [TestFixture]
    public class When_getting_the_current_task_list
    {
        Browser _app;
        BrowserResponse _response;
        dynamic _db;
        readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");

        [Test]
        [Ignore]
        public void CheckForList()
        {
            //Arrange
            _db = Database.Opener.OpenFile(DatabasePath);
            _db.tasks.Insert(taskname: "confirm index list", taskdescription: "Ensure we display a list of tasks");
            var boostrapper = new DefaultNancyBootstrapper();
            _app = new Browser(boostrapper);

            //Act
            _response = _app.Get("/todo/index", with => with.HttpRequest());

            //Assert
            _response.Body["ul"].ShouldExistOnce();
            _response.Body["input#taskName"].ShouldExistOnce();
            _response.Body["input#taskDescription"].ShouldExistOnce();
            _response.Body["input#addTask"].ShouldExistOnce();
            Assert.That(_response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }
}
