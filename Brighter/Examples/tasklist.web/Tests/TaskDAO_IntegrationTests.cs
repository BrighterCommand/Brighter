using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Machine.Specifications;
using tasklist.web.DataAccess;
using tasklist.web.Models;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web.Tests
{
    [Subject(typeof(TasksDAO))]
    public class TaskDAO_IntegrationTests
    {
        static readonly TasksDAO dao = new TasksDAO();
        static readonly TaskListRetriever retriever = new TaskListRetriever();
        static Task newTask;

        Establish context = () =>
        {
            dao.Clear();
            newTask = new Task(taskName: "Test Name", taskDecription: "Task Description");
        };

        Because of = () => dao.Add(newTask);

        It should_add_the_task_into_the_list = () => ((IEnumerable<Task>) retriever.RetrieveTasks()).Any(tsk => tsk.TaskName == newTask.TaskName).ShouldBeTrue();
    }
}