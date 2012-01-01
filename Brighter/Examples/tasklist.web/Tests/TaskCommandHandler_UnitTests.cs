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
}