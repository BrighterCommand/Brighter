// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using SendGrid;
using Tasks.Model;

namespace Tasks.Ports
{
    public class MailTranslator : IAmAMailTranslator
    {
        public Mail Translate(TaskReminder taskReminder)
        {
            var mail = Mail.GetInstance();
            mail.AddTo(taskReminder.ReminderTo);
            mail.AddCc(taskReminder.CopyReminderTo);
            mail.Subject = string.Format("Task Reminder! Task {0} is due on {1}", taskReminder.TaskName, taskReminder.DueDate);
            return mail;
        }
    }
}