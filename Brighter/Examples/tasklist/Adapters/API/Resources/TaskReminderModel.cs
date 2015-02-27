using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Tasklist.Adapters.API.Resources
{
    [DataContract, XmlRoot]
    public class TaskReminderModel
    {
        [DataMember(Name = "copyTo"), XmlElement(ElementName = "copyTo")]
        public string CopyTo { get; set; }
        [DataMember(Name = "dueDate"), XmlElement(ElementName = "dueDate")]
        public string DueDate { get; set; }
        [DataMember(Name = "recipient"), XmlElement(ElementName = "recipient")]
        public string Recipient { get; set; }
        [DataMember(Name = "taskName"), XmlElement(ElementName = "taskName")]
        public string TaskName { get; set; }
    }
}