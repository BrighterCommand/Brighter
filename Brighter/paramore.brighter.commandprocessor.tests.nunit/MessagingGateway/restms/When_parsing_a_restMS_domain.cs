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

using nUnitShouldAdapter;
using NUnit.Framework;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;
using paramore.brighter.commandprocessor.messaginggateway.restms.Parsers;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.restms
{
    [Subject("Messaging Gateway")]
    [Category("RESTMS")]
    [Property("Requires", "RESTMS")]
    public class When_parsing_a_restMS_domain : ContextSpecification
    {
        private const string BODY = "<domain xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" name=\"default\" title=\"title\" href=\"http://localhost/restms/domain/default\" xmlns=\"http://www.restms.org/schema/restms\"><feed type=\"Default\" name=\"default\" title=\"Default feed\" href=\"http://localhost/restms/feed/default\" /><profile name=\"3/Defaults\" href=\"href://www.restms.org/spec:3/Defaults\" /></domain>";
        private static RestMSDomain s_domain;
        private static bool s_couldParse;

        private Because _of = () => s_couldParse = XmlResultParser.TryParse(BODY, out s_domain);

        private It _should_be_able_to_parse_the_result = () => s_couldParse.ShouldBeTrue();
        private It _should_have_a_domain_object = () => s_domain.ShouldNotBeNull();
    }
}
