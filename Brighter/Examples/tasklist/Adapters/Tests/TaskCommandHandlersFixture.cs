using System;
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using Tasklist.Adapters.DataAccess;
using Tasklist.Domain;
using Tasklist.Ports.Commands;
using Tasklist.Ports.Handlers;

namespace Tasklist.Adapters.Tests
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
            tasksDAO = A.Fake<ITasksDAO>( _ => _.Strict());
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored))
                .Invokes(
                    fake => taskToBeAdded = fake.GetArgument<Task>(0)
                );

            cmd = new AddTaskCommand(TASK_NAME, TASK_DESCRIPTION, NOW);

            handler = new AddTaskCommandHandler(tasksDAO, logger);                            
        };

        Because of = () => handler.Handle(cmd);

        It should_add_a_new_task_to_the_db = () => A.CallTo(() => tasksDAO.Add(A<Task>.Ignored)).MustHaveHappened();
        It should_have_the_matching_task_name = () => taskToBeAdded.TaskName.ShouldEqual(TASK_NAME);
        It should_have_the_matching_task_description = () => taskToBeAdded.TaskDescription.ShouldEqual(TASK_DESCRIPTION);
        It sould_have_the_matching_task_name = () => taskToBeAdded.DueDate.Value.ShouldEqual(NOW);
    }

    [Subject(typeof(EditTaskCommandHandler))]
    public class When_editing_an_existing_task
    {
        private static EditTaskCommandHandler handler;
        private static EditTaskCommand cmd;
        private static ITasksDAO tasksDAO;
        private static Task taskToBeEdited;
        private static Task editedTask;
        private const int TASK_ID = 1;
        private const string NEW_TASK_NAME = "New Test Task";
        private const string NEW_TASK_DESCRIPTION = "New Test that we store a Task";
        private static readonly DateTime NEW_TIME = DateTime.Now.AddDays(1);

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            taskToBeEdited = new Task(TASK_ID, "My Task", "My Task Description", DateTime.Now);   
            tasksDAO = A.Fake<ITasksDAO>(_ => _.Strict());
            A.CallTo(() => tasksDAO.FindById(TASK_ID)).Returns(taskToBeEdited);
            A.CallTo(() => tasksDAO.Update(A<Task>.Ignored))
                .Invokes(
                    fake => editedTask = fake.GetArgument<Task>(0)
                );

            cmd = new EditTaskCommand(TASK_ID, NEW_TASK_NAME, NEW_TASK_DESCRIPTION, NEW_TIME);

            handler = new EditTaskCommandHandler(tasksDAO, logger);
        };

        Because of = () => handler.Handle(cmd);

        It should_get_the_task_from_the_db = () => A.CallTo(() => tasksDAO.FindById(TASK_ID)).MustHaveHappened();
        It should_update_the_task = () => A.CallTo(() => tasksDAO.Update(A<Task>.Ignored)).MustHaveHappened(); 
        It should_update_the_task_with_the_new_task_name = () => taskToBeEdited.TaskName.ShouldEqual(NEW_TASK_NAME);
        It should_update_the_task_with_the_new_task_description = () => taskToBeEdited.TaskDescription.ShouldEqual(NEW_TASK_DESCRIPTION);
        It should_update_the_task_with_the_new_task_time = () => taskToBeEdited.DueDate.ShouldEqual(NEW_TIME);
    }

    [Subject(typeof(CompleteTaskCommandHandler))]
    public class When_completing_an_existing_task
    {
        private static CompleteTaskCommandHandler handler;
        private static CompleteTaskCommand cmd;
        private static ITasksDAO tasksDAO;
        private static Task taskToBeCompleted;
        private const int TASK_ID = 1;
        private static readonly DateTime COMPLETION_DATE = DateTime.Now.AddDays(-1);

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            taskToBeCompleted = new Task(TASK_ID, "My Task", "My Task Description", DateTime.Now);   
            tasksDAO = A.Fake<ITasksDAO>(_ => _.Strict());
            A.CallTo(() => tasksDAO.FindById(TASK_ID)).Returns(taskToBeCompleted);
            A.CallTo(() => tasksDAO.Update(A<Task>.Ignored))
                .Invokes(
                    fake => taskToBeCompleted = fake.GetArgument<Task>(0)
                );

            cmd = new CompleteTaskCommand(TASK_ID, COMPLETION_DATE);

            handler = new CompleteTaskCommandHandler(tasksDAO, logger);                                    
        };

        Because of = () => handler.Handle(cmd);

        It should_get_the_task_from_the_db = () => A.CallTo(() => tasksDAO.FindById(TASK_ID)).MustHaveHappened();
        It should_update_the_task = () => A.CallTo(() => tasksDAO.Update(A<Task>.Ignored)).MustHaveHappened();
        private It should_update_the_tasks_completed_date = () => taskToBeCompleted.CompletionDate.ShouldEqual(COMPLETION_DATE);
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
            tasksDAO = A.Fake<ITasksDAO>(_ => _.Strict());
            A.CallTo(() => tasksDAO.FindById(TASK_ID)).Returns(null);
            cmd = new CompleteTaskCommand(TASK_ID, COMPLETION_DATE);

            handler = new CompleteTaskCommandHandler(tasksDAO, logger);                                    
        };

        Because of = () => exception = Catch.Exception(() => handler.Handle(cmd));

        It should_get_the_task_from_the_db = () => A.CallTo(() => tasksDAO.FindById(TASK_ID)).MustHaveHappened();
        It should_fail = () => exception.ShouldBeAssignableTo<ArgumentOutOfRangeException>();
    }
}