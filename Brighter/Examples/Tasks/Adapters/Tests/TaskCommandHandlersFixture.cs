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
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using Tasks.Adapters.DataAccess;
using Tasks.Model;
using Tasks.Ports.Commands;
using Tasks.Ports.Handlers;

namespace Tasks.Adapters.Tests
{
    [Subject(typeof(AddTaskCommandHandler))]
    public class When_adding_a_new_task_to_the_list
    {
        private static AddTaskCommandHandler s_handler;
        private static AddTaskCommand s_cmd;
        private static ITasksDAO s_tasksDAO;
        private static Task s_taskToBeAdded;
        private const string TASK_NAME = "Test task";
        private const string TASK_DESCRIPTION = "Test that we store a task";
        private static readonly DateTime s_NOW = DateTime.Now;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_tasksDAO = new TasksDAO();
            s_tasksDAO.Clear();

            s_cmd = new AddTaskCommand(TASK_NAME, TASK_DESCRIPTION, s_NOW);

            s_handler = new AddTaskCommandHandler(s_tasksDAO, logger);
        };

        private Because _of = () =>
        {
            s_handler.Handle(s_cmd);
            s_taskToBeAdded = s_tasksDAO.FindById(s_cmd.TaskId);
        };

        private It _should_have_the_matching_task_name = () => s_taskToBeAdded.TaskName.ShouldEqual(TASK_NAME);
        private It _should_have_the_matching_task_description = () => s_taskToBeAdded.TaskDescription.ShouldEqual(TASK_DESCRIPTION);
        private It _sould_have_the_matching_task_name = () => s_taskToBeAdded.DueDate.Value.ToShortDateString().ShouldEqual(s_NOW.ToShortDateString());
    }

    [Subject(typeof(EditTaskCommandHandler))]
    public class When_editing_an_existing_task
    {
        private static EditTaskCommandHandler s_handler;
        private static EditTaskCommand s_cmd;
        private static ITasksDAO s_tasksDAO;
        private static Task s_taskToBeEdited;
        private const string NEW_TASK_NAME = "New Test Task";
        private const string NEW_TASK_DESCRIPTION = "New Test that we store a Task";
        private static readonly DateTime s_NEW_TIME = DateTime.Now.AddDays(1);

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_taskToBeEdited = new Task("My Task", "My Task Description", DateTime.Now);
            s_tasksDAO = new TasksDAO();
            s_tasksDAO.Clear();
            s_taskToBeEdited = s_tasksDAO.Add(s_taskToBeEdited);

            s_cmd = new EditTaskCommand(s_taskToBeEdited.Id, NEW_TASK_NAME, NEW_TASK_DESCRIPTION, s_NEW_TIME);

            s_handler = new EditTaskCommandHandler(s_tasksDAO, logger);
        };

        private Because _of = () =>
        {
            s_handler.Handle(s_cmd);
            s_taskToBeEdited = s_tasksDAO.FindById(s_cmd.TaskId);
        };

        private It _should_update_the_task_with_the_new_task_name = () => s_taskToBeEdited.TaskName.ShouldEqual(NEW_TASK_NAME);
        private It _should_update_the_task_with_the_new_task_description = () => s_taskToBeEdited.TaskDescription.ShouldEqual(NEW_TASK_DESCRIPTION);
        private It _should_update_the_task_with_the_new_task_time = () => s_taskToBeEdited.DueDate.Value.ToShortDateString().ShouldEqual(s_NEW_TIME.ToShortDateString());
    }

    [Subject(typeof(CompleteTaskCommandHandler))]
    public class When_completing_an_existing_task
    {
        private static CompleteTaskCommandHandler s_handler;
        private static CompleteTaskCommand s_cmd;
        private static ITasksDAO s_tasksDAO;
        private static Task s_taskToBeCompleted;
        private static readonly DateTime s_COMPLETION_DATE = DateTime.Now.AddDays(-1);

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_taskToBeCompleted = new Task("My Task", "My Task Description", DateTime.Now);
            s_tasksDAO = new TasksDAO();
            s_tasksDAO.Clear();
            s_taskToBeCompleted = s_tasksDAO.Add(s_taskToBeCompleted);

            s_cmd = new CompleteTaskCommand(s_taskToBeCompleted.Id, s_COMPLETION_DATE);

            s_handler = new CompleteTaskCommandHandler(s_tasksDAO, logger);
        };

        private Because _of = () =>
        {
            s_handler.Handle(s_cmd);
            s_taskToBeCompleted = s_tasksDAO.FindById(s_cmd.TaskId);
        };

        private It _should_update_the_tasks_completed_date = () => s_taskToBeCompleted.CompletionDate.Value.ToShortDateString().ShouldEqual(s_COMPLETION_DATE.ToShortDateString());
    }

    [Subject(typeof(CompleteTaskCommandHandler))]
    public class When_completing_a_missing_task
    {
        private static CompleteTaskCommandHandler s_handler;
        private static CompleteTaskCommand s_cmd;
        private static ITasksDAO s_tasksDAO;
        private const int TASK_ID = 1;
        private static readonly DateTime s_COMPLETION_DATE = DateTime.Now.AddDays(-1);
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_tasksDAO = new TasksDAO();
            s_tasksDAO.Clear();
            s_cmd = new CompleteTaskCommand(TASK_ID, s_COMPLETION_DATE);

            s_handler = new CompleteTaskCommandHandler(s_tasksDAO, logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_handler.Handle(s_cmd));

        private It _should_fail = () => s_exception.ShouldBeAssignableTo<ArgumentOutOfRangeException>();
    }
}