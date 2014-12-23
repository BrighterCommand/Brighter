#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using Machine.Specifications;
using Tasklist.Adapters.API.Resources;
using Tasklist.Ports.ViewModelRetrievers;
using Tasks.Adapters.DataAccess;
using Tasks.Model;

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
        private const string hostName = "http://localhost:49743";

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