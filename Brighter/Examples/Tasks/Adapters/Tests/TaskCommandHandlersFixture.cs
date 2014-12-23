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
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using Tasks.Adapters.DataAccess;
using Tasks.Model;
using Tasks.Ports.Commands;
using Tasks.Ports.Handlers;

namespace Tasks.Adapters.Tests
{
    [Subject(typeof(AddTaskCommandHandler))]
    public class When_adding_a_new_task_to_the_list
    {
        private static AddTaskCommandHandler handler;
        private static AddTaskCommand cmd;
        private static ITasksDAO tasksDAO;
        private static Task taskToBeAdded;
        private const string TASK_NAME = "Test task";
        private const string TASK_DESCRIPTION = "Test that we store a task";
        private static readonly DateTime NOW = DateTime.Now;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            tasksDAO = new TasksDAO();
            tasksDAO.Clear();

            cmd = new AddTaskCommand(TASK_NAME, TASK_DESCRIPTION, NOW);

            handler = new AddTaskCommandHandler(tasksDAO, logger);                            
        };

        Because of = () =>
        {
            handler.Handle(cmd);
            taskToBeAdded = tasksDAO.FindById(cmd.TaskId);
        };

        It should_have_the_matching_task_name = () => taskToBeAdded.TaskName.ShouldEqual(TASK_NAME);
        It should_have_the_matching_task_description = () => taskToBeAdded.TaskDescription.ShouldEqual(TASK_DESCRIPTION);
        It sould_have_the_matching_task_name = () => taskToBeAdded.DueDate.Value.ToShortDateString().ShouldEqual(NOW.ToShortDateString());
    }

    [Subject(typeof(EditTaskCommandHandler))]
    public class When_editing_an_existing_task
    {
        private static EditTaskCommandHandler handler;
        private static EditTaskCommand cmd;
        private static ITasksDAO tasksDAO;
        private static Task taskToBeEdited;
        private const string NEW_TASK_NAME = "New Test Task";
        private const string NEW_TASK_DESCRIPTION = "New Test that we store a Task";
        private static readonly DateTime NEW_TIME = DateTime.Now.AddDays(1);

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            taskToBeEdited = new Task("My Task", "My Task Description", DateTime.Now);   
            tasksDAO = new TasksDAO();
            tasksDAO.Clear();
            taskToBeEdited = tasksDAO.Add(taskToBeEdited);

            cmd = new EditTaskCommand(taskToBeEdited.Id, NEW_TASK_NAME, NEW_TASK_DESCRIPTION, NEW_TIME);

            handler = new EditTaskCommandHandler(tasksDAO, logger);
        };

        Because of = () =>
        {
            handler.Handle(cmd);
            taskToBeEdited = tasksDAO.FindById(cmd.TaskId);
        };

        It should_update_the_task_with_the_new_task_name = () => taskToBeEdited.TaskName.ShouldEqual(NEW_TASK_NAME);
        It should_update_the_task_with_the_new_task_description = () => taskToBeEdited.TaskDescription.ShouldEqual(NEW_TASK_DESCRIPTION);
        It should_update_the_task_with_the_new_task_time = () => taskToBeEdited.DueDate.Value.ToShortDateString().ShouldEqual(NEW_TIME.ToShortDateString());
    }

    [Subject(typeof(CompleteTaskCommandHandler))]
    public class When_completing_an_existing_task
    {
        private static CompleteTaskCommandHandler handler;
        private static CompleteTaskCommand cmd;
        private static ITasksDAO tasksDAO;
        private static Task taskToBeCompleted;
        private static readonly DateTime COMPLETION_DATE = DateTime.Now.AddDays(-1);

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            taskToBeCompleted = new Task("My Task", "My Task Description", DateTime.Now);   
            tasksDAO = new TasksDAO();
            tasksDAO.Clear();
            taskToBeCompleted = tasksDAO.Add(taskToBeCompleted);

            cmd = new CompleteTaskCommand(taskToBeCompleted.Id, COMPLETION_DATE);

            handler = new CompleteTaskCommandHandler(tasksDAO, logger);                                    
        };

        Because of = () =>
        {
            handler.Handle(cmd);
            taskToBeCompleted = tasksDAO.FindById(cmd.TaskId);
        };

        It should_update_the_tasks_completed_date = () => taskToBeCompleted.CompletionDate.Value.ToShortDateString().ShouldEqual(COMPLETION_DATE.ToShortDateString());
    }

    [Subject(typeof(CompleteTaskCommandHandler))]
    public class When_completing_a_missing_task
    {
        private static CompleteTaskCommandHandler handler;
        private static CompleteTaskCommand cmd;
        private static ITasksDAO tasksDAO;
        private const int TASK_ID = 1;
        private static readonly DateTime COMPLETION_DATE = DateTime.Now.AddDays(-1);
        private static Exception exception;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            tasksDAO = new TasksDAO();
            tasksDAO.Clear();
            cmd = new CompleteTaskCommand(TASK_ID, COMPLETION_DATE);

            handler = new CompleteTaskCommandHandler(tasksDAO, logger);                                    
        };

        Because of = () => exception = Catch.Exception(() => handler.Handle(cmd));

        It should_fail = () => exception.ShouldBeAssignableTo<ArgumentOutOfRangeException>();
    }
}