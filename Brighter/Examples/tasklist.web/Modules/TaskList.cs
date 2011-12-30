using System.IO;
using System.Reflection;
using Nancy;
using Simple.Data;
using tasklist.web.Models;

namespace tasklist.web.Modules
{
    public class TaskListModule : NancyModule
    {
        static readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");

        public TaskListModule()
        {
            Get["/"] = _ => 
            { 
                var db = Database.Opener.OpenFile(DatabasePath);
                var tasks = db.Tasks.All().ToList<Task>();
                return View["index.sshtml", new { Tasks = tasks }]; 
            };
        }
    }
}