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
        static T SerializeToXml<T>(T value)
        {
            var serializer = new XmlSerializer(typeof (T));
            using (var textWriter = new StringWriter())
            {
                serializer.Serialize(textWriter, value);
                var xml = textWriter.ToString();

                using (var textReader = new StringReader(xml))
                {
                    var obj = (T) serializer.Deserialize(textReader);
                    return obj;
                }
            }
        }

        public class When_serializing_a_domain_to_xml
        {
            static RestMSDomain domain;
            static RestMSDomain newDomain;

            Establish context = () =>
            {
                Globals.HostName = "host.com";
                domain = new RestMSDomain(
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

            Because of = () => newDomain = SerializeToXml(domain);

            It should_set_the_name = () => newDomain .Name.ShouldEqual("default");
            It should_set_the_title = () => newDomain .Title.ShouldEqual("default");
            It should_set_the_profile_name = () => newDomain .Profile.Name.ShouldEqual("default");
            It should_set_the_profile_uri = () => newDomain .Profile.Href.ShouldEqual("http://localhost/restms/domain/default");
        }

        public class When_serializing_a_feed_to_xml
        {
            static RestMSFeed feed;
            static RestMSFeed newFeed;

            Establish context = () =>
            {
                Globals.HostName = "host.com";
                feed = new RestMSFeed(
                    new Feed(
                        new Name("default"),
                        new AggregateVersion(0)
                    )
                );
             };

            Because of = () => newFeed = SerializeToXml(feed);

            It should_set_the_name = () => newFeed.Name.ShouldEqual("default");
        }

        public class When_serializing_a_join_to_xml
        {
            static RestMSJoin join;
            static RestMSJoin newJoin;

            Establish context = () =>
            {
                Globals.HostName = "host.com";
                join = new RestMSJoin()
                {
                    Name = "foo",
                    Address = "http://host.com/restms/join/foo",
                    Feed = "http://host.com/restms/feed/bar",
                    Type = FeedType.Direct.ToString()
                };
            };


            Because of = () => newJoin = SerializeToXml(join);

            It should_set_the_feed_name = () => newJoin.Name.ShouldEqual("foo");
            It should_set_the_address = () => newJoin.Address.ShouldEqual("http://host.com/restms/join/foo");
            It should_set_the_feed = () => newJoin.Feed.ShouldEqual("http://host.com/restms/feed/bar");
            It should_set_the_feed_type = () => newJoin.Type.ShouldEqual("Direct");
        }

        public class When_serializing_a_message_to_xml
        {
            static RestMSMessage message;
            static RestMSMessage newMessage;

            Establish context = () =>
            {
                message = new RestMSMessage(
                    new Message(
                        new Address("*"),
                        new Uri("http://host.com/restms/feed/bar"), 
                        new NameValueCollection() { { "MyHeader", "MyValue" } },
                        Attachment.CreateAttachmentFromString("hello world", MediaTypeNames.Text.Plain)
                        )
                    );
            };

            Because of = () => newMessage = SerializeToXml(message);

            It should_set_the_message_address = () => newMessage.Address.ShouldEqual("*");
            It should_set_the_feed_uri = () => newMessage.Feed.ShouldEqual("http://host.com/restms/feed/bar");
            It should_set_the_header_name = () => newMessage.Headers[0].Name.ShouldEqual("MyHeader");
            It should_set_the_header_value = () => newMessage.Headers[0].Value.ShouldEqual("MyValue");
            It should_set_the_message_content_type = () => newMessage.Content.Type.ShouldEqual("text/plain; name=\"text/plain\"; charset=us-ascii");
            It should_set_the_message_encoding = () => newMessage.Content.Encoding.ShouldEqual("QuotedPrintable");
            It should_set_the_message_content = () => newMessage.Content.Value.ShouldEqual("hello world");
        }

        public class When_serializing_a_message_link_to_xml
        {
            static RestMSMessageLink message;
            static RestMSMessageLink newMessage;
            static Message internalMessage;

            Establish context = () =>
            {
                Globals.HostName = "host.com";
                internalMessage = new Message(
                    new Address("*"),
                    new Uri("http://host.com/restms/feed/bar"), 
                    new NameValueCollection() { { "MyHeader", "MyValue" } },
                    Attachment.CreateAttachmentFromString("hello world", MediaTypeNames.Text.Plain)
                    );

                internalMessage.PipeName = new Name("foobar");
                message = new RestMSMessageLink(internalMessage);
            };

            Because of = () => newMessage = SerializeToXml(message);

            It should_set_the_message_address = () => newMessage.Address.ShouldEqual("*");
            It should_set_the_message_id = () => newMessage.MessageId.ShouldEqual(internalMessage.MessageId.ToString());
            It should_set_the_header_name = () => newMessage.Href.ShouldEqual(string.Format("http://{0}/restms/pipe/{1}/message/{2}", Globals.HostName, "foobar", internalMessage.MessageId));

        }

        public class When_serializing_a_message_posted_count
        {
            static RestMSMessagePosted messagePosted;
            static RestMSMessagePosted newMessagePosted;

            Establish context = () =>
            {
                messagePosted = new RestMSMessagePosted() {Count = 5};
            };

            Because of = () => newMessagePosted = SerializeToXml(messagePosted);

            It should_have_a_matching_count = () => newMessagePosted.Count.ShouldEqual(5);

        }

        public class When_serializing_a_pipe
        {
            static RestMSPipe pipe;
            static RestMSPipe newPipe;
            static Pipe internalPipe;

            Establish context = () =>
            {
                Globals.HostName = "host.com";
                internalPipe = new Pipe(
                    new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                    PipeType.Fifo,
                    new Title("My Title")
                    );
                
                var join = new Join(
                    internalPipe,
                    new Uri("http://host.com/restms/feed/myfeed"),
                    new Address("*"));
                internalPipe.AddJoin(join);

                var message = new Message(
                   new Address("*"),
                   new Uri("http://host.com/restms/feed/bar"),
                   new NameValueCollection(),
                   Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
                   );

                internalPipe.AddMessage(message);
                pipe = new RestMSPipe(internalPipe);
            };

            Because of = () => newPipe = SerializeToXml(pipe);

            It should_have_the_pipe_name = () => newPipe.Name.ShouldEqual("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}");
            It should_have_the_pipe_type = () => newPipe.Type.ShouldEqual("Fifo");
            It should_have_the_pipe_title = () => newPipe.Title.ShouldEqual("My Title");
            It should_have_the_pipe_href = () => newPipe.Href.ShouldEqual(internalPipe.Href.AbsoluteUri);
            It should_have_one_join = () => newPipe.Joins.Count().ShouldEqual(1);
            It should_have_one_message = () => newPipe.Messages.Count().ShouldEqual(1);
        }

        public class When_serializing_a_pipe_link
        {
            static RestMSPipeLink pipe;
            static RestMSPipeLink newPipe;
            static Pipe internalPipe;

            Establish context = () =>
            {
                Globals.HostName = "host.com";
                internalPipe = new Pipe(
                    new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                    PipeType.Fifo,
                    new Title("My Title")
                    );
                
                var join = new Join(
                    internalPipe,
                    new Uri("http://host.com/restms/feed/myfeed"),
                    new Address("*"));
                internalPipe.AddJoin(join);

                var message = new Message(
                   new Address("*"),
                   new Uri("http://host.com/restms/feed/bar"),
                   new NameValueCollection(),
                   Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
                   );

                internalPipe.AddMessage(message);
                pipe = new RestMSPipeLink(internalPipe);
            };

            Because of = () => newPipe = SerializeToXml(pipe);

            It should_have_the_pipe_name = () => newPipe.Name.ShouldEqual("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}");
            It should_have_the_pipe_type = () => newPipe.Type.ShouldEqual("Fifo");
            It should_have_the_pipe_title = () => newPipe.Title.ShouldEqual("My Title");
            It should_have_the_pipe_href = () => newPipe.Href.ShouldEqual(internalPipe.Href.AbsoluteUri);
            
        }

        public class When_serializing_a_new_pipe
        {
            static RestMSPipeNew pipe;
            static RestMSPipeNew newPipe;

            Establish context = () =>
            {
                pipe = new RestMSPipeNew(){ Title = "My Pipe", Type = "Fifo"};
            };

            Because of = () => newPipe = SerializeToXml(pipe);

            It should_have_the_pipe_title = () => newPipe.Title.ShouldEqual("My Pipe");
            It should_have_the_pipe_type = () => newPipe.Type.ShouldEqual("Fifo");
        }

        public class When_parsing_a_new_feed
        {
            const string BODY = "<?xml version=\"1.0\" ?><feed xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" type=\"Direct\" name=\"default\" title=\"\" xmlns=\"http://www.restms.org/schema/restms\" />";
            static RestMSFeed newFeed;
            static readonly XmlDomainPostParser parser = new XmlDomainPostParser();

            Establish context = () =>
            {
                Globals.HostName = "host.com";
                newFeed = null;
            };

            Because of = () =>
                         {
                             var result = parser.Parse(BODY);
                             if (result.Item1 == ParseResult.NewFeed)
                             {
                                 newFeed = result.Item2;
                             }
                         };

            It should_return_a_restms_feed = () => newFeed.ShouldNotBeNull();
        }


    }
}
