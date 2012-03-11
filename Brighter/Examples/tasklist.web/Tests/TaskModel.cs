namespace tasklist.web.Tests
{
    internal class TaskModel
    {
        public TaskModel(string dueDate, string taskDescription, string taskName)
        {
            DueDate = dueDate;
            TaskDescription = taskDescription;
            TaskName = taskName;
        }

        public string DueDate { get; set; }
        public string TaskDescription { get; set; }
        public string TaskName { get; set; }

    }
}