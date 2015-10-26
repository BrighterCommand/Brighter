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
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
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
        private const string taskName = "Test Name";
        private const string taskDescription = "Task Description";
        private static DateTime? s_dueDate = DateTime.Now;

        private Establish _context = () =>
            {
                s_dao = new TasksDAO();
                s_dao.Clear();
                s_newTask = new Task(taskName: taskName, taskDecription: taskDescription, dueDate: s_dueDate);
                var addedTask = s_dao.Add(s_newTask);
                s_taskId = addedTask.Id;

                s_retriever = new TaskListRetriever(hostName, s_dao);
            };

        private Because _of = () => s_taskList = s_retriever.RetrieveTasks();

        private It _should_have_a_link_in_the_list_to_the_task = () => s_taskList.Items.Any(taskLink => taskLink.HRef == string.Format("<link rel=\"item\" href=\"http://{0}/task/{1}\" />", hostName, s_taskId));
        private It _should_have_a_name_for_the_task = () =>
        {
            var task = s_taskList.Items.First();
            task.ShouldNotBeNull();
            task.Name.ShouldEqual(taskName);
        };
        private It _should_have_a_description_for_the_task = () =>
        {
            var task = s_taskList.Items.First(t => t.Id == s_taskId);
            task.ShouldNotBeNull();
            task.Description.ShouldEqual(taskDescription);
        };
        private It _should_have_a_due_date_for_the_task = () =>
        {
            var task = s_taskList.Items.First(t => t.Id == s_taskId);
            task.ShouldNotBeNull();
            task.DueDate.ShouldEqual(s_dueDate.ToDisplayString());
        };
        private It _should_have_null_completion_date_for_the_task = () =>
        {
            var task = s_taskList.Items.First(t => t.Id == s_taskId);
            task.ShouldNotBeNull();
            task.CompletionDate.ShouldBeEmpty();
        };
    }
}