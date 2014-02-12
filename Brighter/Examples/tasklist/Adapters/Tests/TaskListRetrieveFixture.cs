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
        private static TaskListRetriever retriever; 
        static Task newTask;
        private static int taskId;
        private static TaskListModel taskList;
        private static string hostName = "http://localhost:49743";

        Establish context = () =>
            {
                dao = new TasksDAO();
                dao.Clear();
                newTask = new Task(taskName: "Test Name", taskDecription: "Task Description", dueDate: DateTime.Now);
                var addedTask = dao.Add(newTask);
                taskId = addedTask.Id;

                retriever = new TaskListRetriever(hostName);
            };

        Because of = () => taskList = retriever.RetrieveTasks();

        It should_have_a_self_link = () => taskList.Self.ToString().ShouldEqual(string.Format("<link rel=\"self\" href=\"http://{0}/tasks\" />", hostName));
        It should_have_a_link_in_the_list_to_the_task = () => taskList.Links.Any(taskLink => taskLink.HRef == string.Format("<link rel=\"item\" href=\"http://{0}/task/{1}\" />", hostName, taskId));
    }
}