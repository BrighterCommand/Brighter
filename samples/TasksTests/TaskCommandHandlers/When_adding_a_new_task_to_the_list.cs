using System;
using FakeItEasy;
using Xunit;
using Paramore.Brighter;
using Tasks.Adapters.DataAccess;
using Tasks.Model;
using Tasks.Ports.Commands;
using Tasks.Ports.Handlers;

namespace TasksTests.TaskCommandHandlers
{
    public class AddTaskCommandHandlerTests
    {
        private AddTaskCommandHandler _handler;
        private AddTaskCommand _cmd;
        private ITasksDAO _tasksDAO;
        private Task _taskToBeAdded;
        private const string TASK_NAME = "Test task";
        private const string TASK_DESCRIPTION = "Test that we store a task";
        private readonly DateTime _now = DateTime.Now;
        private IAmACommandProcessor _commandProcessor;

        public AddTaskCommandHandlerTests()
        {
            _tasksDAO = new Tasks.Adapters.DataAccess.TasksDAO();
            _tasksDAO.Clear();

            _cmd = new AddTaskCommand(TASK_NAME, TASK_DESCRIPTION, _now);

            _commandProcessor = A.Fake<IAmACommandProcessor>();

            _handler = new AddTaskCommandHandler(_tasksDAO, _commandProcessor);
        }

        [Fact]
        public void When_adding_a_new_task_to_the_list()
        {
            _handler.Handle(_cmd);
            _taskToBeAdded = _tasksDAO.FindById(_cmd.TaskId);

            //_should_have_the_matching_task_name
            Assert.AreEqual(TASK_NAME, _taskToBeAdded.TaskName);
            //_should_have_the_matching_task_description
            Assert.AreEqual(TASK_DESCRIPTION, _taskToBeAdded.TaskDescription);
            //_should_have_the_matching_task_name
            Assert.AreEqual(_now.Date, _taskToBeAdded.DueDate.Value.Date);
        }
    }
}