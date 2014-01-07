using System;
using Machine.Specifications;
using Tasklist.Adapters.DataAccess;
using Tasklist.Domain;

namespace Tasklist.Adapters.Tests
{
    [Subject(typeof(TasksDAO))]
    public class When_updating_a_task
    {
        static TasksDAO dao;
        static Task newTask;
        static Task addedTask;
        static Task foundTask;
        const string NEW_TASK_NAME = "New Task Name";
        const string NEW_TASK_DESCRIPTION = "New Task Description";
        static readonly DateTime? NEW_DUE_DATE = DateTime.Now.AddDays(1);
        static readonly DateTime? NEW_COMPLETION_DATE = DateTime.Now.AddDays(2);

        Establish context = () =>
        {
            dao = new TasksDAO();
            dao.Clear();
            newTask = new Task(taskName: "Test Name", taskDecription: "Task Description", dueDate: DateTime.Now);
            addedTask = dao.Add(newTask);
            addedTask .TaskName = NEW_TASK_NAME;
            addedTask .TaskDescription = NEW_TASK_DESCRIPTION;
            addedTask .DueDate = NEW_DUE_DATE;
            addedTask .CompletionDate = NEW_COMPLETION_DATE;
        };

        private Because of = () => dao.Update(addedTask); 

        It should_add_the_task_into_the_list = () => GetTask().ShouldNotBeNull();
        It should_set_the_task_name = () => GetTask().TaskName.ShouldEqual(NEW_TASK_NAME);
        It should_set_the_task_description = () => GetTask().TaskDescription.ShouldEqual(NEW_TASK_DESCRIPTION); 
        It should_set_the_task_duedate = () => GetTask().DueDate.Value.ToShortDateString().ShouldEqual(NEW_DUE_DATE.Value.ToShortDateString());
        It Should_set_the_task_completion_date = () => GetTask().CompletionDate.Value.ToShortDateString().ShouldEqual(NEW_COMPLETION_DATE.Value.ToShortDateString());

        private static Task GetTask()
        {
            return foundTask ?? (foundTask = dao.FindById(addedTask.Id));
        }
    }
}