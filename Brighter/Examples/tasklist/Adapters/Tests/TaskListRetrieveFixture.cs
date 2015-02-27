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
        private static TasksDAO s_dao;
        private static TaskListRetriever s_retriever;
        private static Task s_newTask;
        private static int s_taskId;
        private static TaskListModel s_taskList;
        private const string hostName = "http://localhost:49743";

        private Establish _context = () =>
            {
                s_dao = new TasksDAO();
                s_dao.Clear();
                s_newTask = new Task(taskName: "Test Name", taskDecription: "Task Description", dueDate: DateTime.Now);
                var addedTask = s_dao.Add(s_newTask);
                s_taskId = addedTask.Id;

                s_retriever = new TaskListRetriever(hostName);
            };

        private Because _of = () => s_taskList = s_retriever.RetrieveTasks();

        private It _should_have_a_self_link = () => s_taskList.Self.ToString().ShouldEqual(string.Format("<link rel=\"self\" href=\"http://{0}/tasks\" />", hostName));
        private It _should_have_a_link_in_the_list_to_the_task = () => s_taskList.Links.Any(taskLink => taskLink.HRef == string.Format("<link rel=\"item\" href=\"http://{0}/task/{1}\" />", hostName, s_taskId));
    }
}