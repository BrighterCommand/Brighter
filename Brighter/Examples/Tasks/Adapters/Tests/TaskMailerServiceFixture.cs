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
using SendGrid;
using Tasks.Model;
using Tasks.Ports;

namespace Tasks.Adapters.Tests
{
    public class When_marshalling_a_task_reminder_to_a_sendgrid_mail
    {
        static Mail mailMessage;
        static TaskReminder taskReminder;
        static EmailAddress recipient;
        static EmailAddress copyTo;
        static TaskName taskName;
        static DateTime dueDate;

        Establish context = () =>
        {
            dueDate = DateTime.UtcNow.AddDays(1);
            recipient= new EmailAddress("ian.hammond.cooper@gmail.com");
            copyTo = new EmailAddress("ian@huddle.net");
            taskName = new TaskName("My Task");


            taskReminder = new TaskReminder(
                taskName: new TaskName(taskName ),
                dueDate: dueDate,
                reminderTo: recipient,
                copyReminderTo: copyTo);
            
        };

        Because of = () => mailMessage = new MailTranslator().Translate(taskReminder);

        It should_have_the_correct_subject = () => mailMessage.Subject.ShouldEqual(string.Format("Task Reminder! Task {0} is due on {1}", taskName, dueDate));
        It should_have_the_correct_recipient_addressee = () => mailMessage.To[0].Address.ShouldEqual(recipient);
        It should_have_the_correct_to_address = () => mailMessage.Cc[0].Address.ShouldEqual(copyTo);
    }
}
