using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Machine.Specifications;
using Simple.Data;
using tasklist.web.DataAccess;
using tasklist.web.Models;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web.Tests
{
    [Subject(typeof(TasksDAO))]
    public class When_retrieving_a_list_of_tasks
    {
        static readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");
        static TasksDAO dao;
        static readonly TaskListRetriever retriever = new TaskListRetriever();
        static Task newTask;

        Establish context = () =>
        {
            dao = new TasksDAO();
            dao.Db = Database.Opener.OpenFile(DatabasePath);
            dao.Clear();
            newTask = new Task(taskName: "Test Name", taskDecription: "Task Description");
        };

        Because of = () => dao.Add(newTask);

        It should_add_the_task_into_the_list = () => ((IEnumerable<Task>) retriever.RetrieveTasks()).Any(tsk => tsk.TaskName == newTask.TaskName).ShouldBeTrue();
    }

    [Subject(typeof(TasksDAO))]
    public class When_retrieving_a_task
    {
        static readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");
        static TasksDAO dao;
        static readonly TaskRetriever retriever = new TaskRetriever();
        static Task newTask;

        Establish context = () =>
        {
            dao = new TasksDAO();
            dao.Db = Database.Opener.OpenFile(DatabasePath);
            dao.Clear();
            newTask = new Task(id: 1, taskName: "Test Name", taskDecription: "Task Description");
        };

        Because of = () => dao.Add(newTask);

        It should_add_the_task_into_the_list = () => retriever.Get(newTask.Id).ShouldNotBeNull();
    }
}