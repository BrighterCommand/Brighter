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

using MailKit.Net.Smtp;
using Tasks.Model;
using Tasks.Ports;


namespace Tasks.Adapters.MailGateway
{
    public class MailGateway : IAmAMailGateway
    {
        private readonly IAmAMailTranslator _translator;
        private readonly SmtpDetails _smtpDetails;

        public MailGateway(IAmAMailTranslator translator, SmtpDetails smtpDetails)
        {
            _translator = translator;
            _smtpDetails = smtpDetails;
        }

        public void Send(TaskReminder reminder)
        {
            var message = _translator.Translate(reminder);

            using (var client = new SmtpClient())
            {

                //_smtpDetails = new SmtpDetails()
                //{
                //    Server = "smtp.friends.com",
                //    Port = 587,
                //    UserName = "joey",
                //    Password = "password"
                //};


                client.Connect(_smtpDetails.Server, _smtpDetails.Port, false);

                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");

                // Note: only needed if the SMTP server requires authentication
                client.Authenticate(_smtpDetails.UserName, _smtpDetails.Password);

                client.Send(message);
                client.Disconnect(true);
            }
        }
    }

    public class SmtpDetails
    {
        public string Server { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
