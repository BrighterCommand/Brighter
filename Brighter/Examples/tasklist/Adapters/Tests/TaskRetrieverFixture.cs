using System;
using Machine.Specifications;
using Tasklist.Adapters.DataAccess;
using Tasklist.Domain;
using Tasklist.Ports.ViewModelRetrievers;

namespace Tasklist.Adapters.Tests
{
    [Subject(typeof(TasksDAO))]
    public class When_retrieving_a_task
    {
        static TasksDAO dao;
        static readonly TaskRetriever retriever = new TaskRetriever();
        static Task newTask;
        static Task addedTask;

        Establish context = () =>
            {
                dao = new TasksDAO();
                dao.Clear();
                newTask = new Task(taskName: "Test Name", taskDecription: "Task Description", dueDate: DateTime.Now);
            };

        Because of = () => addedTask = dao.Add(newTask);

        It should_add_the_task_into_the_list = () => retriever.Get(addedTask.Id).ShouldNotBeNull();
    }
}