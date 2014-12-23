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
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Tasks.Model;

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