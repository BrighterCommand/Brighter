// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Tasks.Model;
using Task = System.Threading.Tasks.Task;

namespace Tasks.Adapters.MailGateway
{
    public interface IAmAMailGateway
    {
        Task Send(TaskReminder reminder);
    }
}