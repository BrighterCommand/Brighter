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
using Machine.Specifications;
using Tasks.Adapters.DataAccess;
using Tasks.Model;

namespace Tasks.Adapters.Tests
{
    [Subject(typeof(TasksDAO))]
    public class When_updating_a_task
    {
        static TasksDAO dao;
        static Task newTask;
        static Task addedTask;
        static Task foundTask;
        const string NEW_TASK_NAME = "New Task Name";
        const string NEW_TASK_DESCRIPTION = "New Task Description";
        static readonly DateTime? NEW_DUE_DATE = DateTime.Now.AddDays(1);
        static readonly DateTime? NEW_COMPLETION_DATE = DateTime.Now.AddDays(2);

        Establish context = () =>
        {
            dao = new TasksDAO();
            dao.Clear();
            newTask = new Task(taskName: "Test Name", taskDecription: "Task Description", dueDate: DateTime.Now);
            addedTask = dao.Add(newTask);
            addedTask .TaskName = NEW_TASK_NAME;
            addedTask .TaskDescription = NEW_TASK_DESCRIPTION;
            addedTask .DueDate = NEW_DUE_DATE;
            addedTask .CompletionDate = NEW_COMPLETION_DATE;
        };

        private Because of = () => dao.Update(addedTask); 

        It should_add_the_task_into_the_list = () => GetTask().ShouldNotBeNull();
        It should_set_the_task_name = () => GetTask().TaskName.ShouldEqual(NEW_TASK_NAME);
        It should_set_the_task_description = () => GetTask().TaskDescription.ShouldEqual(NEW_TASK_DESCRIPTION); 
        It should_set_the_task_duedate = () => GetTask().DueDate.Value.ToShortDateString().ShouldEqual(NEW_DUE_DATE.Value.ToShortDateString());
        It Should_set_the_task_completion_date = () => GetTask().CompletionDate.Value.ToShortDateString().ShouldEqual(NEW_COMPLETION_DATE.Value.ToShortDateString());

        private static Task GetTask()
        {
            return foundTask ?? (foundTask = dao.FindById(addedTask.Id));
        }
    }
}