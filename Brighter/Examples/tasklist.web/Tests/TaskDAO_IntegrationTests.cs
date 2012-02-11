using System.Collections.Generic;
using System.Linq;
using Machine.Specifications;
using tasklist.web.DataAccess;
using tasklist.web.Models;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web.Tests
{
    [Subject(typeof(TasksDAO))]
    public class When_retrieving_a_list_of_tasks
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

    [Subject(typeof(TasksDAO))]
    public class When_retrieving_a_task
    {
        static readonly TasksDAO dao = new TasksDAO();
        static readonly TaskRetriever retriever = new TaskRetriever();
        static Task newTask;

        Establish context = () =>
        {
            dao.Clear();
            newTask = new Task(taskName: "Test Name", taskDecription: "Task Description");
        };

        Because of = () => dao.Add(newTask);

        It should_add_the_task_into_the_list = () => ((IEnumerable<Task>) retriever.Get(newTask.Id)).Any(tsk => tsk.TaskName == newTask.TaskName).ShouldBeTrue();
    }
}