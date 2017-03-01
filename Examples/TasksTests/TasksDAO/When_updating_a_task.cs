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
using NUnit.Framework;
using Tasks.Model;

namespace TasksTests.TasksDAO
{
    [TestFixture]
    public class TasksDAOUpdateTests
    {
        private Tasks.Adapters.DataAccess.TasksDAO _dao;
        private Task _newTask;
        private Task _addedTask;
        private const string NEW_TASK_NAME = "New Task Name";
        private const string NEW_TASK_DESCRIPTION = "New Task Description";
        private readonly DateTime? s_NEW_DUE_DATE = DateTime.Now.AddDays(1);
        private readonly DateTime? s_NEW_COMPLETION_DATE = DateTime.Now.AddDays(2);

        [Test]
        public void Establish()
        {
            _dao = new Tasks.Adapters.DataAccess.TasksDAO();
            _dao.Clear();
            _newTask = new Task(taskName: "Test Name", taskDecription: "Task Description", dueDate: DateTime.Now);
            _addedTask = _dao.Add(_newTask);
            _addedTask.TaskName = NEW_TASK_NAME;
            _addedTask.TaskDescription = NEW_TASK_DESCRIPTION;
            _addedTask.DueDate = s_NEW_DUE_DATE;
            _addedTask.CompletionDate = s_NEW_COMPLETION_DATE;
        }

        [Test]
        public void When_updating_a_task()
        {
            _dao.Update(_addedTask);

            //_should_add_the_task_into_the_list
            GetTask().ShouldNotBeNull();
            //_should_set_the_task_name
            GetTask().TaskName.ShouldEqual(NEW_TASK_NAME);
            //_should_set_the_task_description
            GetTask().TaskDescription.ShouldEqual(NEW_TASK_DESCRIPTION);
            //_should_set_the_task_duedate
            GetTask().DueDate.Value.Date.ShouldEqual(s_NEW_DUE_DATE.Value.Date);
            //_should_set_the_task_completion_date
            GetTask().CompletionDate.Value.Date.ShouldEqual(s_NEW_COMPLETION_DATE.Value.Date);
        }

        private Task GetTask()
        {
            return _dao.FindById(_addedTask.Id) ?? (_dao.FindById(_addedTask.Id));
        }
    }
}