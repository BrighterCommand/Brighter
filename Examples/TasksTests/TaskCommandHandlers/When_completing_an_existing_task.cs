using System;
using FakeItEasy;
using NUnit.Framework;
using paramore.brighter.commandprocessor;
using Tasks.Adapters.DataAccess;
using Tasks.Model;
using Tasks.Ports.Commands;
using Tasks.Ports.Events;
using Tasks.Ports.Handlers;

namespace TasksTests.TaskCommandHandlers
{
    [TestFixture]
    public class  CompletedTaskHandlerTests
    {
        private CompleteTaskCommandHandler _handler;
        private CompleteTaskCommand _cmd;
        private ITasksDAO _tasksDAO;
        private Task _taskToBeCompleted;
        private readonly DateTime s_COMPLETION_DATE = DateTime.Now.AddDays(-1);
        private IAmACommandProcessor s_commandProcessor;

        [SetUp]
        public void Establish()
        {
            _taskToBeCompleted = new Task("My Task", "My Task Description", DateTime.Now);
            _tasksDAO = new Tasks.Adapters.DataAccess.TasksDAO();
            _tasksDAO.Clear();
            _taskToBeCompleted = _tasksDAO.Add(_taskToBeCompleted);

            _cmd = new CompleteTaskCommand(_taskToBeCompleted.Id, s_COMPLETION_DATE);

            s_commandProcessor = A.Fake<IAmACommandProcessor>();

            _handler = new CompleteTaskCommandHandler(_tasksDAO, s_commandProcessor);
        }

        [Test]
        public void When_completing_an_existing_task()
        {
            _handler.Handle(_cmd);
            _taskToBeCompleted = _tasksDAO.FindById(_cmd.TaskId);

            //_should_update_the_tasks_completed_date
            Assert.AreEqual(s_COMPLETION_DATE.Date, _taskToBeCompleted.CompletionDate.Value.Date);
            //_should_post_event
            A.CallTo(() => s_commandProcessor.Post(A<TaskCompletedEvent>._)).MustHaveHappened(Repeated.Exactly.Once);
        }

    }

}