using System;
using FakeItEasy;
using Machine.Specifications;
using tasklist.web.Commands;
using tasklist.web.DataAccess;
using tasklist.web.Handlers;
using tasklist.web.Models;

namespace tasklist.web.Tests
{
    [Subject(typeof(AddTaskCommandHandler))]
    public class When_adding_a_new_task_to_the_list
    {
        static AddTaskCommandHandler handler;
        static AddTaskCommand cmd;
        static ITasksDAO tasksDAO;

        Establish context = () =>
        {
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));

            cmd = new AddTaskCommand("Test task", "Test that we store a task");

            handler = new AddTaskCommandHandler(tasksDAO);                            
        };

        Because of = () => handler.Handle(cmd);

        It should_add_a_new_task_to_the_db = () => A.CallTo(() => tasksDAO.Add(A<Task>.Ignored)).MustHaveHappened();
    }

    //TODO: This tests need to hit the command processor and build the pipeling, along with registration, not
    // work off the handler.

    [Subject(typeof(AddTaskCommandHandler))]
    public class When_a_new_task_is_missing_the_description
    {
        static AddTaskCommandHandler handler;
        static AddTaskCommand cmd;
        static ITasksDAO tasksDAO;
        static Exception exception;

        Establish context = () =>
        {
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));

            cmd = new AddTaskCommand("Test task", null);

            handler = new AddTaskCommandHandler(tasksDAO);                            
        };

        Because of = () => exception = Catch.Exception(() => handler.Handle(cmd));

        private It should_throw_a_validation_exception = () => exception.ShouldNotBeNull();
    }

    [Subject(typeof(AddTaskCommandHandler))]
    public class When_a_new_task_is_missing_the_name
    {
        static AddTaskCommandHandler handler;
        static AddTaskCommand cmd;
        static ITasksDAO tasksDAO;
        static Exception exception;

        Establish context = () =>
        {
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));

            cmd = new AddTaskCommand(null, "Test that we store a task");

            handler = new AddTaskCommandHandler(tasksDAO);                            
        };

        Because of = () => exception = Catch.Exception(() => handler.Handle(cmd));

        private It should_throw_a_validation_exception = () => exception.ShouldNotBeNull();
    }
}