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
using System.Collections.Specialized;
using System.Linq;
using System.Net.Mime;
using System.Net.Mail;
using FakeItEasy;
using FluentAssertions;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.restms.core;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Handlers;
using paramore.brighter.restms.core.Ports.Repositories;
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.core.Ports.ViewModelRetrievers;
using Message = paramore.brighter.restms.core.Model.Message;

namespace paramore.commandprocessor.tests.RestMSServer
{
    public class When_retrieving_a_message
    {
        private const string ADDRESS_PATTERN = "*";
        private const string MESSAGE_CONTENT = "I am some message content";
        private static Message s_message;
        private static MessageRetriever s_messageRetriever;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static RestMSMessage s_restMSMessage;
        private static Pipe s_pipe;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));

            s_message = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection() { { "MyHeader", "MyValue" } },
                Attachment.CreateAttachmentFromString(MESSAGE_CONTENT, MediaTypeNames.Text.Plain),
                "http://host.com/");

            s_pipe.AddMessage(s_message);

            s_pipeRepository = new InMemoryPipeRepository(logger);
            s_pipeRepository.Add(s_pipe);

            s_messageRetriever = new MessageRetriever(s_pipeRepository);
        };

        private Because _of = () => s_restMSMessage = s_messageRetriever.Retrieve(s_pipe.Name, s_message.MessageId);

        private It _should_have_the_message_address_pattern = () => s_restMSMessage.Address.ShouldEqual(s_message.Address.Value);
        private It _should_have_the_message_id = () => s_restMSMessage.MessageId.ShouldEqual(s_message.MessageId.ToString());
        private It _should_have_the_reply_to = () => s_restMSMessage.ReplyTo.ShouldEqual(s_message.ReplyTo.AbsoluteUri);
        private It _should_have_the_originating_feed_uri = () => s_restMSMessage.Feed.ShouldEqual(s_message.FeedHref.AbsoluteUri);
        private It _should_have_the_header_name_of_the_message = () => s_restMSMessage.Headers[0].Name.ShouldEqual(s_message.Headers.All.First().Item1);
        private It _should_have_the_header_value_of_the_message = () => s_restMSMessage.Headers[0].Value.ShouldEqual(s_message.Headers.All.First().Item2);
        private It _should_have_the_message_content = () => s_restMSMessage.Content.Value.ShouldEqual(MESSAGE_CONTENT);
    }


    public class When_retrieving_a_missing_message
    {
        private const string ADDRESS_PATTERN = "*";
        private const string MESSAGE_CONTENT = "I am some message content";
        private static Message s_message;
        private static MessageRetriever s_messageRetriever;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static Pipe s_pipe;
        private static bool s_exceptionWasThrown;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_exceptionWasThrown = false;

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));

            s_message = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection() { { "MyHeader", "MyValue" } },
                Attachment.CreateAttachmentFromString(MESSAGE_CONTENT, MediaTypeNames.Text.Plain),
                "http://host.com/");

            s_pipeRepository = new InMemoryPipeRepository(logger);
            s_pipeRepository.Add(s_pipe);

            s_messageRetriever = new MessageRetriever(s_pipeRepository);
        };


        private Because _of = () => { try { s_messageRetriever.Retrieve(s_pipe.Name, s_message.MessageId); } catch (MessageDoesNotExistException) { s_exceptionWasThrown = true; } };

        private It _should_throw_an_exception = () => s_exceptionWasThrown.ShouldBeTrue();
    }

    public class When_deleting_a_message
    {
        //deletes the message and all older messages from the pipe.
        //When the server deletes a message it also deletes any contents that message contains.

        private const string ADDRESS_PATTERN = "*";
        private const string MESSAGE_CONTENT = "I am some message content";
        private static Pipe s_pipe;
        private static Message s_message;
        private static Message s_olderMessage;
        private static Message s_newerMessage;
        private static DeleteMessageCommandHandler s_deleteMessageCommandHandler;
        private static DeleteMessageCommand s_deleteMessageCommand;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();

            s_pipeRepository = new InMemoryPipeRepository(logger);

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));

            s_pipeRepository.Add(s_pipe);

            var join = new Join(
                s_pipe,
                feed.Href,
                new Address(ADDRESS_PATTERN));
            s_pipe.AddJoin(join);

            s_olderMessage = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection() { { "MyHeader", "MyValue" } },
                Attachment.CreateAttachmentFromString(MESSAGE_CONTENT, MediaTypeNames.Text.Plain),
                "http://host.com/");
            s_pipe.AddMessage(s_olderMessage);


            s_message = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection() { { "MyHeader", "MyValue" } },
                Attachment.CreateAttachmentFromString(MESSAGE_CONTENT, MediaTypeNames.Text.Plain),
                "http://host.com/");
            s_pipe.AddMessage(s_message);

            //delay long enough to ensure last message is 'newer'
            System.Threading.Tasks.Task.Delay(1.Seconds()).Wait();

            s_newerMessage = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection() { { "MyHeader", "MyValue" } },
                Attachment.CreateAttachmentFromString(MESSAGE_CONTENT, MediaTypeNames.Text.Plain),
                "http://host.com/");
            s_pipe.AddMessage(s_newerMessage);

            s_deleteMessageCommand = new DeleteMessageCommand(s_pipe.Name.Value, s_message.MessageId);
            s_deleteMessageCommandHandler = new DeleteMessageCommandHandler(s_pipeRepository, s_commandProcessor, logger);
        };

        private Because _of = () => s_deleteMessageCommandHandler.Handle(s_deleteMessageCommand);

        private It _should_delete_the_message = () => s_pipe.Messages.Any(msg => msg.MessageId == s_message.MessageId).ShouldBeFalse();
        private It _should_delete_the_older_message = () => s_pipe.Messages.Any(msg => msg.MessageId == s_olderMessage.MessageId).ShouldBeFalse();
        private It _should_not_delete_the_newer_message = () => s_pipe.Messages.Any(msg => msg.MessageId == s_newerMessage.MessageId).ShouldBeTrue();
        private It _should_invalidate_the_pipe_in_the_cache = () => A.CallTo(() => s_commandProcessor.Send(A<InvalidateCacheCommand>.Ignored)).MustHaveHappened();
    }

    public class When_deleting_the_only_message
    {
        //deletes the message and all older messages from the pipe.
        //When the server deletes a message it also deletes any contents that message contains.

        private const string ADDRESS_PATTERN = "*";
        private const string MESSAGE_CONTENT = "I am some message content";
        private static Pipe s_pipe;
        private static Message s_message;
        private static DeleteMessageCommandHandler s_deleteMessageCommandHandler;
        private static DeleteMessageCommand s_deleteMessageCommand;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();

            s_pipeRepository = new InMemoryPipeRepository(logger);

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));

            s_pipeRepository.Add(s_pipe);

            var join = new Join(
                s_pipe,
                feed.Href,
                new Address(ADDRESS_PATTERN));
            s_pipe.AddJoin(join);

            s_message = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection() { { "MyHeader", "MyValue" } },
                Attachment.CreateAttachmentFromString(MESSAGE_CONTENT, MediaTypeNames.Text.Plain),
                "http://host.com/");
            s_pipe.AddMessage(s_message);

            s_deleteMessageCommand = new DeleteMessageCommand(s_pipe.Name.Value, s_message.MessageId);
            s_deleteMessageCommandHandler = new DeleteMessageCommandHandler(s_pipeRepository, s_commandProcessor, logger);
        };

        private Because _of = () => s_deleteMessageCommandHandler.Handle(s_deleteMessageCommand);

        private It _should_delete_the_message = () => s_pipe.Messages.Any(msg => msg.MessageId == s_message.MessageId).ShouldBeFalse();
        private It _should_invalidate_the_pipe_in_the_cache = () => A.CallTo(() => s_commandProcessor.Send(A<InvalidateCacheCommand>.Ignored)).MustHaveHappened();
    }
}
