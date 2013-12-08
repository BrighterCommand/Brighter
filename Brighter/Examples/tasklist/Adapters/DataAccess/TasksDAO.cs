using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using Simple.Data;
using Tasklist.Domain;

namespace Tasklist.Adapters.DataAccess
{
    public class TasksDAO : ITasksDAO
    {
        private dynamic db;

        public TasksDAO()
        {

            if (System.Web.HttpContext.Current != null)
            {
                var databasePath = System.Web.HttpContext.Current.Server.MapPath("~\\App_Data\\Tasks.sdf");
                db = Database.Opener.OpenFile(databasePath);
            }
            else
            {
                var file =  Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase.Substring(8)), "App_Data\\Tasks.sdf");
    
                db = Database.OpenFile(file);
            }
        }
 
        public Task Add(Task newTask)
        {
            return db.Tasks.Insert(newTask);
        }

        public void Update(Task task)
        {
            db.Tasks.UpdateById(task);
        }

        public void Clear()
        {
            db.Tasks.DeleteAll();
        }

        public Task FindById(int taskId)
        {
            return db.Tasks.FindById(taskId);
        }

        public Task FindByName(string taskName)
        {
            return db.Tasks.FindBy(taskName: taskName);
        }

    }
}