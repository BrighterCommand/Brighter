using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Xml.Serialization;
using Machine.Specifications;
using paramore.brighter.restms.core;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;

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


        public class When_serializing_amessage_to_xml
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

    }
}
