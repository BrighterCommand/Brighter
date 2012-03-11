using System;

namespace tasklist.web.Models
{
    public class Task
    {
        public Task() {/*Needed for Simple.Data*/ }
        public Task(string taskName, string taskDecription, DateTime? dueDate = null)
        {
            TaskDescription = taskDecription;
            DueDate = dueDate;
            TaskName = taskName;
        }

        //allow us to set the key as Simple.Data.SQLite does not return a value on insert
        public Task(int id, string taskName, string taskDecription, DateTime? dueDate = null)
        {
            DueDate = dueDate;
            Id = id;
            TaskDescription = taskDecription;
            TaskName = taskName;
        }

        public DateTime? CompletionDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int Id { get; set; }
        public string TaskDescription { get; set; }
        public string TaskName{get; set;}
    }
}