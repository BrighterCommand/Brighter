using System;
using FakeItEasy;
using Xunit;
using Paramore.Brighter;
using Tasks.Adapters.DataAccess;
using Tasks.Model;
using Tasks.Ports.Commands;
using Tasks.Ports.Events;
using Tasks.Ports.Handlers;

namespace TasksTests.TaskCommandHandlers
{
    public class EditTaskCommandHandlerTests
    {
        private EditTaskCommandHandler _handler;
        private EditTaskCommand _cmd;
        private ITasksDAO _tasksDAO;
        private Task _taskToBeEdited;
        private const string NEW_TASK_NAME = "New Test Task";
        private const string NEW_TASK_DESCRIPTION = "New Test that we store a Task";
        private readonly DateTime _NEW_TIME = DateTime.Now.AddDays(1);
        private IAmACommandProcessor s_commandProcessor;

        public EditTaskCommandHandlerTests()
        {
            _taskToBeEdited = new Task("My Task", "My Task Description", DateTime.Now);
            _tasksDAO = new Tasks.Adapters.DataAccess.TasksDAO();
            _tasksDAO.Clear();
            _taskToBeEdited = _tasksDAO.Add(_taskToBeEdited);

            _cmd = new EditTaskCommand(_taskToBeEdited.Id, NEW_TASK_NAME, NEW_TASK_DESCRIPTION, _NEW_TIME);

            s_commandProcessor = A.Fake<IAmACommandProcessor>();

            _handler = new EditTaskCommandHandler(_tasksDAO, s_commandProcessor);
        }

        [Fact]
        public void When_editing_an_existing_task()
        {
            _handler.Handle(_cmd);
            _taskToBeEdited = _tasksDAO.FindById(_cmd.TaskId);

            //_should_update_the_task_with_the_new_task_name
            Assert.AreEqual(NEW_TASK_NAME, _taskToBeEdited.TaskName);
            //_should_update_the_task_with_the_new_task_description
            Assert.AreEqual(NEW_TASK_DESCRIPTION, _taskToBeEdited.TaskDescription);
            //_should_update_the_task_with_the_new_task_time
            Assert.AreEqual(_NEW_TIME.Date, _taskToBeEdited.DueDate.Value.Date);
            //_should_post_event
            A.CallTo(() => s_commandProcessor.Post(A<TaskEditedEvent>._)).MustHaveHappened(Repeated.Exactly.Once);
        }
    }
}