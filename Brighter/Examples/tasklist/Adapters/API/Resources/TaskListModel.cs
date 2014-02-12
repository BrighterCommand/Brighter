using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Tasklist.Domain;

namespace Tasklist.Adapters.API.Resources
{
    [DataContract, XmlRoot]
    public class TaskListModel
    {
        private Link self;
        private IEnumerable<Link> links; 

        public TaskListModel(IEnumerable<Task> tasks, string hostName)
        {
            self = Link.Create(this, hostName);
            links = tasks.Select(task => Link.Create((Task)task, hostName));
        }

        [DataMember(Name = "self"), XmlElement(ElementName = "self")]
        public Link Self
        {
            get { return self; }
            set { self = value; }
        }

        [DataMember(Name = "links"), XmlElement(ElementName = "links")]
        public IEnumerable<Link> Links
        {
            get { return links; }
            set { links = value; }
        }
    }
}