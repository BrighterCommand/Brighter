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
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Xml.Serialization;
using Machine.Specifications;
using paramore.brighter.restms.core;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.server.Adapters.Formatters;

namespace paramore.commandprocessor.tests.RestMSServer
{
    public class SerializationTests
    {
        private static T SerializeToXml<T>(T value)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var textWriter = new StringWriter())
            {
                serializer.Serialize(textWriter, value);
                var xml = textWriter.ToString();

                using (var textReader = new StringReader(xml))
                {
                    var obj = (T)serializer.Deserialize(textReader);
                    return obj;
                }
            }
        }

        public class When_serializing_a_domain_to_xml
        {
            private static RestMSDomain s_domain;
            private static RestMSDomain s_newDomain;

            private Establish _context = () =>
            {
                Globals.HostName = "host.com";
                s_domain = new RestMSDomain(
                    new Domain(
                        new Name("default"),
                        new Title("default"),
                        new Profile(
                            new Name("default"),
                            new Uri("http://localhost/restms/domain/default")
                        )
                     ),
                 new Feed[0],
                 new Pipe[0]
                 );
            };

            private Because _of = () => s_newDomain = SerializeToXml(s_domain);

            private It _should_set_the_name = () => s_newDomain.Name.ShouldEqual("default");
            private It _should_set_the_title = () => s_newDomain.Title.ShouldEqual("default");
            private It _should_set_the_profile_name = () => s_newDomain.Profile.Name.ShouldEqual("default");
            private It _should_set_the_profile_uri = () => s_newDomain.Profile.Href.ShouldEqual("http://localhost/restms/domain/default");
        }

        public class When_serializing_a_feed_to_xml
        {
            private static RestMSFeed s_feed;
            private static RestMSFeed s_newFeed;

            private Establish _context = () =>
            {
                Globals.HostName = "host.com";
                s_feed = new RestMSFeed(
                    new Feed(
                        new Name("default"),
                        new AggregateVersion(0)
                    )
                );
            };

            private Because _of = () => s_newFeed = SerializeToXml(s_feed);

            private It _should_set_the_name = () => s_newFeed.Name.ShouldEqual("default");
        }

        public class When_serializing_a_join_to_xml
        {
            private static RestMSJoin s_join;
            private static RestMSJoin s_newJoin;

            private Establish _context = () =>
            {
                Globals.HostName = "host.com";
                s_join = new RestMSJoin()
                {
                    Name = "foo",
                    Address = "http://host.com/restms/join/foo",
                    Feed = "http://host.com/restms/feed/bar",
                    Type = FeedType.Direct.ToString()
                };
            };


            private Because _of = () => s_newJoin = SerializeToXml(s_join);

            private It _should_set_the_feed_name = () => s_newJoin.Name.ShouldEqual("foo");
            private It _should_set_the_address = () => s_newJoin.Address.ShouldEqual("http://host.com/restms/join/foo");
            private It _should_set_the_feed = () => s_newJoin.Feed.ShouldEqual("http://host.com/restms/feed/bar");
            private It _should_set_the_feed_type = () => s_newJoin.Type.ShouldEqual("Direct");
        }

        public class When_serializing_a_message_to_xml
        {
            private static RestMSMessage s_message;
            private static RestMSMessage s_newMessage;

            private Establish _context = () =>
            {
                s_message = new RestMSMessage(
                    new Message(
                        new Address("*"),
                        new Uri("http://host.com/restms/feed/bar"),
                        new NameValueCollection() { { "MyHeader", "MyValue" } },
                        Attachment.CreateAttachmentFromString("hello world", MediaTypeNames.Text.Plain)
                        )
                    );
            };

            private Because _of = () => s_newMessage = SerializeToXml(s_message);

            private It _should_set_the_message_address = () => s_newMessage.Address.ShouldEqual("*");
            private It _should_set_the_feed_uri = () => s_newMessage.Feed.ShouldEqual("http://host.com/restms/feed/bar");
            private It _should_set_the_header_name = () => s_newMessage.Headers[0].Name.ShouldEqual("MyHeader");
            private It _should_set_the_header_value = () => s_newMessage.Headers[0].Value.ShouldEqual("MyValue");
            private It _should_set_the_message_content_type = () => s_newMessage.Content.Type.ShouldEqual("text/plain; name=\"text/plain\"; charset=us-ascii");
            private It _should_set_the_message_encoding = () => s_newMessage.Content.Encoding.ShouldEqual("QuotedPrintable");
            private It _should_set_the_message_content = () => s_newMessage.Content.Value.ShouldEqual("hello world");
        }

        public class When_serializing_a_message_link_to_xml
        {
            private static RestMSMessageLink s_message;
            private static RestMSMessageLink s_newMessage;
            private static Message s_internalMessage;

            private Establish _context = () =>
            {
                Globals.HostName = "host.com";
                s_internalMessage = new Message(
                    new Address("*"),
                    new Uri("http://host.com/restms/feed/bar"),
                    new NameValueCollection() { { "MyHeader", "MyValue" } },
                    Attachment.CreateAttachmentFromString("hello world", MediaTypeNames.Text.Plain)
                    );

                s_internalMessage.PipeName = new Name("foobar");
                s_message = new RestMSMessageLink(s_internalMessage);
            };

            private Because _of = () => s_newMessage = SerializeToXml(s_message);

            private It _should_set_the_message_address = () => s_newMessage.Address.ShouldEqual("*");
            private It _should_set_the_message_id = () => s_newMessage.MessageId.ShouldEqual(s_internalMessage.MessageId.ToString());
            private It _should_set_the_header_name = () => s_newMessage.Href.ShouldEqual(string.Format("http://{0}/restms/pipe/{1}/message/{2}", Globals.HostName, "foobar", s_internalMessage.MessageId));
        }

        public class When_serializing_a_message_posted_count
        {
            private static RestMSMessagePosted s_messagePosted;
            private static RestMSMessagePosted s_newMessagePosted;

            private Establish _context = () =>
            {
                s_messagePosted = new RestMSMessagePosted() { Count = 5 };
            };

            private Because _of = () => s_newMessagePosted = SerializeToXml(s_messagePosted);

            private It _should_have_a_matching_count = () => s_newMessagePosted.Count.ShouldEqual(5);
        }

        public class When_serializing_a_pipe
        {
            private static RestMSPipe s_pipe;
            private static RestMSPipe s_newPipe;
            private static Pipe s_internalPipe;

            private Establish _context = () =>
            {
                Globals.HostName = "host.com";
                s_internalPipe = new Pipe(
                    new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                    PipeType.Fifo,
                    new Title("My Title")
                    );

                var join = new Join(
                    s_internalPipe,
                    new Uri("http://host.com/restms/feed/myfeed"),
                    new Address("*"));
                s_internalPipe.AddJoin(join);

                var message = new Message(
                   new Address("*"),
                   new Uri("http://host.com/restms/feed/bar"),
                   new NameValueCollection(),
                   Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
                   );

                s_internalPipe.AddMessage(message);
                s_pipe = new RestMSPipe(s_internalPipe);
            };

            private Because _of = () => s_newPipe = SerializeToXml(s_pipe);

            private It _should_have_the_pipe_name = () => s_newPipe.Name.ShouldEqual("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}");
            private It _should_have_the_pipe_type = () => s_newPipe.Type.ShouldEqual("Fifo");
            private It _should_have_the_pipe_title = () => s_newPipe.Title.ShouldEqual("My Title");
            private It _should_have_the_pipe_href = () => s_newPipe.Href.ShouldEqual(s_internalPipe.Href.AbsoluteUri);
            private It _should_have_one_join = () => s_newPipe.Joins.Count().ShouldEqual(1);
            private It _should_have_one_message = () => s_newPipe.Messages.Count().ShouldEqual(1);
        }

        public class When_serializing_a_pipe_link
        {
            private static RestMSPipeLink s_pipe;
            private static RestMSPipeLink s_newPipe;
            private static Pipe s_internalPipe;

            private Establish _context = () =>
            {
                Globals.HostName = "host.com";
                s_internalPipe = new Pipe(
                    new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                    PipeType.Fifo,
                    new Title("My Title")
                    );

                var join = new Join(
                    s_internalPipe,
                    new Uri("http://host.com/restms/feed/myfeed"),
                    new Address("*"));
                s_internalPipe.AddJoin(join);

                var message = new Message(
                   new Address("*"),
                   new Uri("http://host.com/restms/feed/bar"),
                   new NameValueCollection(),
                   Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
                   );

                s_internalPipe.AddMessage(message);
                s_pipe = new RestMSPipeLink(s_internalPipe);
            };

            private Because _of = () => s_newPipe = SerializeToXml(s_pipe);

            private It _should_have_the_pipe_name = () => s_newPipe.Name.ShouldEqual("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}");
            private It _should_have_the_pipe_type = () => s_newPipe.Type.ShouldEqual("Fifo");
            private It _should_have_the_pipe_title = () => s_newPipe.Title.ShouldEqual("My Title");
            private It _should_have_the_pipe_href = () => s_newPipe.Href.ShouldEqual(s_internalPipe.Href.AbsoluteUri);
        }

        public class When_serializing_a_new_pipe
        {
            private static RestMSPipeNew s_pipe;
            private static RestMSPipeNew s_newPipe;

            private Establish _context = () =>
            {
                s_pipe = new RestMSPipeNew() { Title = "My Pipe", Type = "Fifo" };
            };

            private Because _of = () => s_newPipe = SerializeToXml(s_pipe);

            private It _should_have_the_pipe_title = () => s_newPipe.Title.ShouldEqual("My Pipe");
            private It _should_have_the_pipe_type = () => s_newPipe.Type.ShouldEqual("Fifo");
        }

        public class When_parsing_a_new_feed
        {
            private const string BODY = "<?xml version=\"1.0\" ?><feed xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" type=\"Direct\" name=\"default\" title=\"\" xmlns=\"http://www.restms.org/schema/restms\" />";
            private static RestMSFeed s_newFeed;
            private static readonly XmlDomainPostParser s_parser = new XmlDomainPostParser();

            private Establish _context = () =>
            {
                Globals.HostName = "host.com";
                s_newFeed = null;
            };

            private Because _of = () =>
                         {
                             var result = s_parser.Parse(BODY);
                             if (result.Item1 == ParseResult.NewFeed)
                             {
                                 s_newFeed = result.Item2;
                             }
                         };

            private It _should_return_a_restms_feed = () => s_newFeed.ShouldNotBeNull();
        }
    }
}
