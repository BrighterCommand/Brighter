namespace tasklist.web.Models
{
    public class Task
    {
        public Task() {/*Needed for Simple.Data*/ }
        public Task(string taskName, string taskDecription)
        {
            TaskName = taskName;
            TaskDescription = taskDecription;
        }

        //allow us to set the key as Simple.Data.SQLite does not return a value on insert
        public Task(int id, string taskName, string taskDecription)
        {
            Id = id;
            TaskName = taskName;
            TaskDescription = taskDecription;
        }

        public string TaskDescription { get; set; }
        public int Id { get; set; }
        public string TaskName{get; set;}
    }
}