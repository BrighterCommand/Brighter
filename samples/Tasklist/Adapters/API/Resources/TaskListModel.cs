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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Tasks.Model;

namespace Tasklist.Adapters.API.Resources
{
    [DataContract, XmlRoot]
    public class TaskListModel
    {
        private Link _self;
        private IEnumerable<TaskListItemModel> _items;

        public TaskListModel(IEnumerable<Task> tasks, string hostName)
        {
            _self = Link.Create(this, hostName);
            _items = tasks.Select(task => TaskListItemModel.Create(task, hostName));
        }

        [DataMember(Name = "self"), XmlElement(ElementName = "self")]
        public Link Self
        {
            get { return _self; }
            set { _self = value; }
        }

        [DataMember(Name = "items"), XmlElement(ElementName = "items")]
        public IEnumerable<TaskListItemModel> Items
        {
            get { return _items; }
            set { _items = value; }
        }
    }

    [DataContract, XmlRoot]
    public class TaskListItemModel
    {
        [DataMember(Name = "self"), XmlElement(ElementName = "self")]
        public Link Self{ get; private set; }
        [DataMember(Name = "name"), XmlElement(ElementName = "name")]
        public string Name { get; private set; }
        [DataMember(Name = "description"), XmlElement(ElementName = "description")]
        public string Description { get; private set; }
        [DataMember(Name = "completionDate"), XmlElement(ElementName = "completionDate")]
        public string CompletionDate { get; private set; }
        [DataMember(Name = "dueDate"), XmlElement(ElementName = "dueDate")]
        public string DueDate { get; private set; }
        [DataMember(Name = "href"), XmlElement(ElementName = "href")]
        public string HRef { get; private set; }
        [DataMember(Name = "isComplete"), XmlElement(ElementName = "isComplete")]
        public bool IsComplete { get; set; }
        [DataMember(Name = "id"), XmlElement(ElementName = "id")]
        public int Id { get; set; }

        public static TaskListItemModel Create(Task task, string hostName)
        {
            var hRef = string.Format("http://{0}/{1}/{2}", hostName, "task", task.Id);
            return new TaskListItemModel
            {
                HRef = hRef,
                Self = new Link { Rel = "item", HRef = hRef },
                Name = task.TaskName,
                Description = task.TaskDescription,
                IsComplete = task.CompletionDate.HasValue,
                CompletionDate = task.CompletionDate.ToDisplayString(),
                DueDate = task.DueDate.ToDisplayString(),
                Id = task.Id
            };
        }

    }

    public static class Extensions
    {
        public static string ToDisplayString(this DateTime? dateTimeToFormat)
        {
            if (!dateTimeToFormat.HasValue) return "";
            return dateTimeToFormat.Value.ToString("dd-MMM-yyyy HH:mm:ss");
        }
    }
}