using Machine.Specifications;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;
using paramore.brighter.commandprocessor.messaginggateway.restms.Parsers;

namespace paramore.commandprocessor.tests.MessagingGateway.restms
{
    public class When_parsing_a_restMS_domain
    {
        const string BODY = "<domain xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" name=\"default\" title=\"title\" href=\"http://localhost/restms/domain/default\" xmlns=\"http://www.restms.org/schema/restms\"><feed type=\"Default\" name=\"default\" title=\"Default feed\" href=\"http://localhost/restms/feed/default\" /><profile name=\"3/Defaults\" href=\"href://www.restms.org/spec:3/Defaults\" /></domain>";
        static RestMSDomain domain;
        static bool couldParse;

        Because of = () => couldParse = XmlResultParser.TryParse(BODY, out domain);

        It should_be_able_to_parse_the_result = () => couldParse.ShouldBeTrue();
        It should_have_a_domain_object = () => domain.ShouldNotBeNull();
    }
}
