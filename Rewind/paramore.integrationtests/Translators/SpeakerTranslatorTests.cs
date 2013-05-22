using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Machine.Specifications;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Adapters.Presentation.API.Resources;
using Paramore.Adapters.Presentation.API.Translators;
using Paramore.Domain.Common;
using Paramore.Domain.Speakers;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace paramore.integrationtests.Translators
{
   [Subject("Check that we can get the venue list out of the thin read layer")]
    public class When_changing_a_speakerdocument_to_a_resource
    {
        static readonly SpeakerTranslator venueTranslator = new SpeakerTranslator();
        static SpeakerDocument document;
        static SpeakerResource resource;

        Establish context = () =>
            {
                document = new SpeakerDocument(
                    id: new Id(Guid.NewGuid()),
                    version: new Version(1), 
                    bio: new SpeakerBio("Alt.NET purse fighter"),
                    phoneNumber: new PhoneNumber("11111-111111"),
                    emailAddress: new EmailAddress("dude@speakers.net"),
                    name: new SpeakerName("The Dude") 
                    );
            };

        Because of = () => resource = venueTranslator.Translate(document);

        It should_set_the_self_uri = () => resource[ParamoreGlobals.Self].ToString().ShouldEqual(string.Format("<link rel=\"self\" href=\"http://{0}/speaker/{1}\" />", ParamoreGlobals.HostName, document.Id));
        It should_set_the_version = () => resource.Version.ShouldEqual(document.Version);
        It should_set_the_speaker_name = () => resource.Name.ShouldEqual(document.Name);
        It should_set_the_phone_number = () => resource.PhoneNumber.ShouldEqual(document.PhoneNumber);
        It should_set_the_emailAddress = () => resource.EmailAddress.ShouldEqual(document.Email);
        It should_set_the_bio = () => resource.Bio.ShouldEqual(document.Bio);

    }

     [Subject("Check that we serialize to the expected xml")]
    public class When_serializing_a_speaker_to_xml
    {
        static XmlSerializer serializer;
        static StringWriter stringwriter;
        static SpeakerResource resource;
        static string response;

        Establish context = () =>
            {
                stringwriter = new StringWriter();
                resource = new SpeakerResource(
                    id: Guid.NewGuid(),
                    version: 1,
                    name: "The Dude",
                    phoneNumber: "11111-111111",
                    emailAddress: "dude@speakers.net",
                    bio: "Alt.NET purse fighter"
                );
                serializer = new XmlSerializer(typeof(SpeakerResource));
            };

        Because of = () =>
            {
                using (var writer = new XmlTextWriter(stringwriter) { Formatting = Formatting.Indented })
                {
                    serializer.Serialize(writer, resource);
                }
                response =  stringwriter.GetStringBuilder().ToString();
            };

        It should_format_the_self_uri_as_expected = () => response.ShouldContain(string.Format("<link rel=\"self\" href=\"http://{0}/speaker/{1}\" />", ParamoreGlobals.HostName, resource.Id));
        It should_format_the_speakerName = () => response.ShouldContain("<name>The Dude</name>");
        It should_format_the_emailAddress = () => response.ShouldContain("<emailAddress>dude@speakers.net</emailAddress>");
        It should_format_the_phoneNumber = () => response.ShouldContain("<phoneNumber>11111-111111</phoneNumber>");
        It should_format_the_bio= () => response.ShouldContain("<bio>Alt.NET purse fighter</bio>");
        It should_format_the_version_as_expected = () => response.ShouldContain(string.Format("<version>{0}</version>", resource.Version)); 
    }
}
