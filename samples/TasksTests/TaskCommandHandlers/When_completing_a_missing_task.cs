using System;
using FakeItEasy;
using FluentAssertions;
using Xunit;
using Paramore.Brighter;
using Tasks.Adapters.DataAccess;
using Tasks.Ports.Commands;
using Tasks.Ports.Events;
using Tasks.Ports.Handlers;

namespace TasksTests.TaskCommandHandlers
{
    public class CompleteTaskCommandHandlerTests
    {
        private CompleteTaskCommandHandler _handler;
        private CompleteTaskCommand _cmd;
        private ITasksDAO _tasksDAO;
        private const int TASK_ID = 1;
        private readonly DateTime _COMPLETION_DATE = DateTime.Now.AddDays(-1);
        private Exception _exception;
        private IAmACommandProcessor _commandProcessor;

        public CompleteTaskCommandHandlerTests()
        {
            _tasksDAO = new Tasks.Adapters.DataAccess.TasksDAO();
            _tasksDAO.Clear();
            _cmd = new CompleteTaskCommand(TASK_ID, _COMPLETION_DATE);

            _commandProcessor = A.Fake<IAmACommandProcessor>();

            A.CallTo(() => _commandProcessor.Post(A<TaskCompletedEvent>._));

            _handler = new CompleteTaskCommandHandler(_tasksDAO, _commandProcessor);
        }

        [Fact]
        public void When_completing_a_missing_task()
        {
            _exception = Catch.Exception(() => _handler.Handle(_cmd));

            //_should_fail
            _exception.Should().BeOfType<ArgumentOutOfRangeException>();
            //_should_not_post_event
            A.CallTo(() => _commandProcessor.Post(A<TaskCompletedEvent>._)).MustNotHaveHappened();
        }
    }
}