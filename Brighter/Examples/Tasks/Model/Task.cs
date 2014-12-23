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

namespace Tasks.Model
{
    public class Task
    {
        public Task() {/*Needed for Simple.Data*/ }
        public Task(string taskName, string taskDecription, DateTime? dueDate = null)
        {
            TaskDescription = taskDecription;
            DueDate = dueDate;
            TaskName = taskName;
        }

        //allow us to set the key as Simple.Data.SQLite does not return a value on insert
        public Task(int id, string taskName, string taskDecription, DateTime? dueDate = null)
        {
            DueDate = dueDate;
            Id = id;
            TaskDescription = taskDecription;
            TaskName = taskName;
        }

        public DateTime? CompletionDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int Id { get; set; }
        public string TaskDescription { get; set; }
        public string TaskName{get; set;}
    }
}