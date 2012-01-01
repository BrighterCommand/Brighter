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

        public string TaskName{get; set;}
        public string TaskDescription { get; set; }
    }
}