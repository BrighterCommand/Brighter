using System;
using System.IO;
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
            
        }

    }
}
