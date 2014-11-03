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
using System.Collections.Specialized;
using System.Linq;
using System.Net.Mime;
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using System.Net.Mail;
using paramore.brighter.restms.core;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Repositories;
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.core.Ports.ViewModelRetrievers;

namespace paramore.commandprocessor.tests.RestMSServer
{
    public class When_retrieving_a_message
    {
        const string ADDRESS_PATTERN = "*";
        const string MESSAGE_CONTENT = "I am some message content";
        static Message message;
        static MessageRetriever messageRetriever;
        static IAmARepository<Message> messageRepository; 
        static RestMSMessage restMSMessage;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            message = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection() {{"MyHeader", "MyValue"}},
                Attachment.CreateAttachmentFromString(MESSAGE_CONTENT, MediaTypeNames.Text.Plain),
                "http://host.com/");

            messageRepository = new InMemoryMessageRepository(logger);
            messageRepository.Add(message);

            messageRetriever = new MessageRetriever(messageRepository);
        };

        Because of = () => restMSMessage = messageRetriever.Retrieve(message.Name);

        It should_have_the_message_address_pattern = () => restMSMessage.Address.ShouldEqual(message.Address.Value);
        It should_have_the_message_id = () => restMSMessage.MessageId.ShouldEqual(message.MessageId.ToString());
        It should_have_the_reply_to = () => restMSMessage.ReplyTo.ShouldEqual(message.ReplyTo.AbsoluteUri);
        It should_have_the_originating_feed_uri = () => restMSMessage.Feed.ShouldEqual(message.FeedHref.AbsoluteUri);
        It should_have_the_header_name_of_the_message = () => restMSMessage.Headers[0].Name.ShouldEqual(message.Headers.All.First().Item1);
        It should_have_the_header_value_of_the_message = () => restMSMessage.Headers[0].Value.ShouldEqual(message.Headers.All.First().Item2);
        It should_have_the_message_content = () => restMSMessage.Content.Value.ShouldEqual(MESSAGE_CONTENT);
    }


    public class When_retrieving_a_missing_message
    {
        
    }

    public class When_deleting_a_message
    {
        //deletes the message and all older messages from the pipe.
        //When the server deletes a message it also deletes any contents that message contains.
    }
}
