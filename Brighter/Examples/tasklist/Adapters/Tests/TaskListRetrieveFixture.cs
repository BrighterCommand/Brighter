using System;
using System.Collections.Generic;
using System.Linq;
using Machine.Specifications;
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
        private static TaskList taskList;

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

    internal class TaskList
    {
        private readonly Link self;
        private readonly IEnumerable<Link> links; 

        public TaskList(IEnumerable<Task> tasks)
        {
            self = Link.Create(this);
            links = tasks.Select(task => Link.Create(task));
        }

        public Link Self
        {
            get { return self; }
        }

        public IEnumerable<Link> Links
        {
            get { return links; }
        }
    }

    internal class Link
    {
        public Link(string relName, string href)
        {
            this.Rel = relName;
            this.HRef = href;
        }

        public Link()
        {
            //Required for serialiazation
        }

        public string Rel { get; set; }
        public string HRef { get; set; }

        public static Link Create(Task task)
        {
            var link = new Link
                {
                    Rel = "item",
                    HRef = string.Format("http://{0}/{1}/{2}", TaskListGlobals.HostName, "task", task.Id)
                };
            return link;
        }

        public static Link Create(TaskList taskList)
        {
            //we don't need to use taskList to build the self link
            var self = new Link
                {
                    Rel = "self",
                    HRef = string.Format("http://{0}/{1}", TaskListGlobals.HostName, "tasks")
                };

            return self;
        }

        public override string ToString()
        {
            return string.Format("<link rel=\"{0}\" href=\"{1}\" />", Rel, HRef);
        }
    }
}

    internal class TaskListGlobals
    {
        private const string hostName = "localhost:49743";

        public static string HostName
        {
            get { return HostName; }
        }
    }