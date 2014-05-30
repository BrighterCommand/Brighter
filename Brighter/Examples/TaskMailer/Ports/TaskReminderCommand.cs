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
using paramore.brighter.commandprocessor;

namespace TaskMailer.Ports
{
    //For simpliciy here we don't show a common domain model being shared by TaskMailer and TaskList
    //But in practice, if these were within one Business Capability (and Bounded Context) we would have a common model
    //Tasks.Domain that both projects depended on, allowing the command and mappers to exist on both sides
    //Across Bounded Contexts we would not share the model, and each side would map to and from their own model
    //as we do here
    public class TaskReminderCommand : Command
    {
        public TaskReminderCommand(Guid id) : base(id) {}

        public string TaskName { get; set; }
        public DateTime DueDate { get; set; }
        public string Recipient { get; set; }
        public string CopyTo { get; set; }
    }
}