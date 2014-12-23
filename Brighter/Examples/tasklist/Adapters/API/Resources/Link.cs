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

using Tasks.Model;

namespace Tasklist.Adapters.API.Resources
{
    public class Link
    {
        public Link(string relName, string href)
        {
            this.Rel = relName;
            this.HRef = href;
        }

        public Link()
        {
            //Required for serialiazation
        }

        public string Rel { get; set; }
        public string HRef { get; set; }

        public static Link Create(Task task, string hostName)
        {
            var link = new Link
                {
                    Rel = "item",
                    HRef = string.Format("http://{0}/{1}/{2}", hostName, "task", task.Id)
                };
            return link;
        }

        public static Link Create(TaskListModel taskList, string hostName)
        {
            //we don't need to use taskList to build the self link
            var self = new Link
                {
                    Rel = "self",
                    HRef = string.Format("http://{0}/{1}", hostName, "tasks")
                };

            return self;
        }

        public override string ToString()
        {
            return string.Format("<link rel=\"{0}\" href=\"{1}\" />", Rel, HRef);
        }
    }
}