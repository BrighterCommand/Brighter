#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Tasks.Model;

namespace Tasks.Adapters.DataAccess
{
    public class TasksDAO : ITasksDAO
    {
        private string _connectionString = "Data Source = tasks.db";

        public TasksDAO()
        {

        }

        public Task Add(Task newTask)
        {
            using (var context = TasksContextFactory.Create(_connectionString))
            {
                var task = context.Add(newTask);
                context.SaveChanges();

                return task.Entity;
            }
        }

        public IEnumerable<Task> FindAll()
        {
            using (var context = TasksContextFactory.Create(_connectionString))
            {
                return context.Tasks.ToList();
            }
        }

        public void Update(Task task)
        {
            using (var context = TasksContextFactory.Create(_connectionString))
            {
                context.Update(task);
                context.SaveChanges();
            }
        }

        public void Clear()
        {
            using (var context = TasksContextFactory.Create(_connectionString))
            {
                context.Tasks.RemoveRange(context.Tasks);
                context.SaveChanges();
            }
        }

        public Task FindById(int taskId)
        {
            using (var context = TasksContextFactory.Create(_connectionString))
            {
                return context.Tasks.FirstOrDefault(t => t.Id == taskId);

            }
        }

        public Task FindByName(string taskName)
        {

            using (var context = TasksContextFactory.Create(_connectionString))
            {
                return context.Tasks.FirstOrDefault(t => t.TaskName == taskName);
            }

        }
    }
}