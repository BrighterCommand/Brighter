// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using Machine.Specifications;
using Tasklist.Ports.ViewModelRetrievers;
using Tasks.Adapters.DataAccess;
using Tasks.Model;

namespace Tasklist.Adapters.Tests
{
    [Subject(typeof(TasksDAO))]
    public class When_retrieving_a_task
    {
        private static TasksDAO s_dao;
        private static readonly TaskRetriever s_retriever = new TaskRetriever();
        private static Task s_newTask;
        private static Task s_addedTask;

        private Establish _context = () =>
            {
                s_dao = new TasksDAO();
                s_dao.Clear();
                s_newTask = new Task(taskName: "Test Name", taskDecription: "Task Description", dueDate: DateTime.Now);
            };

        private Because _of = () => s_addedTask = s_dao.Add(s_newTask);

        private It _should_add_the_task_into_the_list = () => s_retriever.Get(s_addedTask.Id).ShouldNotBeNull();
    }
}