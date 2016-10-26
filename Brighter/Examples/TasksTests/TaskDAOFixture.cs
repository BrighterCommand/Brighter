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
using nUnitShouldAdapter;
using NUnit.Specifications;
using Tasks.Adapters.DataAccess;
using Tasks.Model;

namespace TasksTests
{
    [Subject(typeof(TasksDAO))]
    public class When_updating_a_task : ContextSpecification
    {
        private static TasksDAO s_dao;
        private static Task s_newTask;
        private static Task s_addedTask;
        private static Task s_foundTask;
        private const string NEW_TASK_NAME = "New Task Name";
        private const string NEW_TASK_DESCRIPTION = "New Task Description";
        private static readonly DateTime? s_NEW_DUE_DATE = DateTime.Now.AddDays(1);
        private static readonly DateTime? s_NEW_COMPLETION_DATE = DateTime.Now.AddDays(2);

        private Establish _context = () =>
        {
            s_dao = new TasksDAO();
            s_dao.Clear();
            s_newTask = new Task(taskName: "Test Name", taskDecription: "Task Description", dueDate: DateTime.Now);
            s_addedTask = s_dao.Add(s_newTask);
            s_addedTask.TaskName = NEW_TASK_NAME;
            s_addedTask.TaskDescription = NEW_TASK_DESCRIPTION;
            s_addedTask.DueDate = s_NEW_DUE_DATE;
            s_addedTask.CompletionDate = s_NEW_COMPLETION_DATE;
        };

        private Because _of = () => s_dao.Update(s_addedTask);

        private It _should_add_the_task_into_the_list = () => GetTask().ShouldNotBeNull();
        private It _should_set_the_task_name = () => GetTask().TaskName.ShouldEqual(NEW_TASK_NAME);
        private It _should_set_the_task_description = () => GetTask().TaskDescription.ShouldEqual(NEW_TASK_DESCRIPTION);
        private It _should_set_the_task_duedate = () => GetTask().DueDate.Value.Date.ShouldEqual(s_NEW_DUE_DATE.Value.Date);
        private It _should_set_the_task_completion_date = () => GetTask().CompletionDate.Value.Date.ShouldEqual(s_NEW_COMPLETION_DATE.Value.Date);

        private static Task GetTask()
        {
            return s_foundTask ?? (s_foundTask = s_dao.FindById(s_addedTask.Id));
        }
    }
}