using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Tasklist.Adapters.API.Resources
{
    [DataContract, XmlRoot]
    public class TaskModel
    {
        public TaskModel(string dueDate, string taskDescription, string taskName)
        {
            DueDate = dueDate;
            TaskDescription = taskDescription;
            TaskName = taskName;
        }

        [DataMember(Name = "dueDate"), XmlElement(ElementName = "dueDate")]
        public string DueDate { get; set; }
        [DataMember(Name = "taskDescription"), XmlElement(ElementName = "taskDescription")]
        public string TaskDescription { get; set; }
        [DataMember(Name = "taskName"), XmlElement(ElementName = "taskName")]
        public string TaskName { get; set; }
    }
}