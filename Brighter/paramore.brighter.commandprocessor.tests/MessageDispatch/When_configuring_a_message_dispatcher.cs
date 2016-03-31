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

using System.Collections.Generic;
using System.Linq;
using Machine.Specifications;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.ServiceActivatorConfiguration;
using paramore.brighter.serviceactivator.TestHelpers;

namespace paramore.commandprocessor.tests.MessageDispatch
{
    /*
    <connections>
      <add connectionName ="foo" channelName="mary" routingKey="bob" isDurable="false" dataType="paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyEvent" noOfPerformers="1" timeOutInMilliseconds="200" />
      <add connectionName ="bar" channelName="alice" routingKey="simon" isDurable="true" dataType="paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyEvent" noOfPerformers="2" timeOutInMilliseconds="100" isAsync="true" />
    </connections>
    */

    public class When_Configuring_A_Message_Dispatcher
    {
        private static IEnumerable<ConnectionElement> s_connectionElements;
        private static IEnumerable<Connection> s_connections;
        private static ConnectionFactory s_connectionFactory;

        private Establish _configuration = () =>
        {
            var configuration = ServiceActivatorConfigurationSection.GetConfiguration();
            s_connectionElements = from ConnectionElement connectionElement in configuration.Connections select connectionElement;
            s_connectionFactory = new ConnectionFactory(new InMemoryChannelFactory());
        };

        private Because _of = () => s_connections = s_connectionFactory.Create(s_connectionElements);

        private It _should_have_two_connections_in_the_list = () => s_connections.Count().ShouldEqual(2);
        private It _should_have_a_foo_connection = () => GetConnection("foo").ShouldNotBeNull();
        private It _should_have_a_foo_connection_with_name_mary = () => GetConnection("foo").ChannelName.Value.ShouldEqual("mary");
        private It _should_have_a_bar_connection_with_name_alice = () => GetConnection("bar").ChannelName.Value.ShouldEqual("alice");
        private It _should_have_a_foo_connection_with_my_event_type = () => GetConnection("foo").DataType.FullName.ShouldEqual("paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyEvent");
        private It _should_have_a_bar_connection_with_my_event_type = () => GetConnection("bar").DataType.FullName.ShouldEqual("paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyEvent");
        private It _should_have_a_foo_connection_with_one_performer = () => GetConnection("foo").NoOfPeformers.ShouldEqual(1);
        private It _should_have_a_bar_connection_with_two_performers = () => GetConnection("bar").NoOfPeformers.ShouldEqual(2);
        private It _should_have_a_foo_connection_with_timeoutInMillisecondsOf_200 = () => GetConnection("foo").TimeoutInMiliseconds.ShouldEqual(200);
        private It _should_have_a_bar_connection_with_timeoutInMillisecondsOf_100 = () => GetConnection("bar").TimeoutInMiliseconds.ShouldEqual(100);
        private It _should_have_a_foo_connection_with_async_false = () => GetConnection("foo").IsAsync.ShouldBeFalse();
        private It _should_have_a_bar_connection_with_async_true = () => GetConnection("bar").IsAsync.ShouldBeTrue();

        private static Connection GetConnection(string name)
        {
            return s_connections.SingleOrDefault(connection => connection.Name == name);
        }
    }
}
