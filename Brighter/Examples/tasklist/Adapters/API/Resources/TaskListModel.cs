using System.Collections.Generic;
using System.Linq;
using Tasklist.Domain;

namespace Tasklist.Adapters.API.Resources
{
    public class TaskListModel
    {
        private readonly Link self;
        private readonly IEnumerable<Link> links; 

        public TaskListModel(IEnumerable<Task> tasks)
        {
            self = Link.Create(this);
            links = tasks.Select(task => Link.Create((Task) task));
        }

        public Link Self
        {
            get { return self; }
        }

        public IEnumerable<Link> Links
        {
            get { return links; }
        }
    }
}