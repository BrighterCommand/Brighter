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

    [Subject(typeof(EditTaskCommandHandler))]
    public class When_editing_an_existing_task
    {
        Establish context;
        Because of;
        It should_get_the_task_from_the_db;
        It should_update_the_task_with_changes;
    }
}