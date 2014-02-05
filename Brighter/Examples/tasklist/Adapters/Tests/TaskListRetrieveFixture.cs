using System;
using System.Linq;
using Machine.Specifications;
using Tasklist.Adapters.API.Resources;
using Tasklist.Adapters.DataAccess;
using Tasklist.Domain;
using Tasklist.Ports.ViewModelRetrievers;

namespace Tasklist.Adapters.Tests
{
    [Subject(typeof(TasksDAO))]
    public class When_retrieving_a_list_of_tasks
    {
        static TasksDAO dao;
        static readonly TaskListRetriever retriever = new TaskListRetriever();
        static Task newTask;
        private static int taskId;
        private static TaskListModel taskList;

        Establish context = () =>
            {
                dao = new TasksDAO();
                dao.Clear();
                newTask = new Task(taskName: "Test Name", taskDecription: "Task Description", dueDate: DateTime.Now);
                var addedTask = dao.Add(newTask);
                taskId = addedTask.Id;
            };

        Because of = () => taskList = retriever.RetrieveTasks();

        It should_have_a_self_link = () => taskList.Self.ToString().ShouldEqual(string.Format("<link rel=\"self\" href=\"http://{0}/tasks\" />",TaskListGlobals.HostName));
        It should_have_a_link_in_the_list_to_the_task = () => taskList.Links.Any(taskLink => taskLink.HRef == string.Format("<link rel=\"item\" href=\"http://{0}/task/{1}\" />", TaskListGlobals.HostName, taskId));
    }
}