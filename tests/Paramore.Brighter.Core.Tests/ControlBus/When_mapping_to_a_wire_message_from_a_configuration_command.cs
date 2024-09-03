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

using FluentAssertions;
using Xunit;
using Paramore.Brighter.ServiceActivator.Ports;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class ConfigurationCommandToMessageMapperTests
    {
        private readonly IAmAMessageMapper<ConfigurationCommand> _mapper;
        private Message _message;
        private readonly ConfigurationCommand _command;
        private readonly Publication _publication;

        public ConfigurationCommandToMessageMapperTests()
        {
            _mapper = new ConfigurationCommandMessageMapper();

            _command = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL, new SubscriptionName("getallthethings"));
            
            _publication = new Publication { Topic = new RoutingKey("ConfigurationCommand") };
        }


        [Fact]
        public void When_mapping_to_a_wire_message_from_a_configuration_command()
        {
            _message = _mapper.MapToMessage(_command, _publication);

            // _should_serialize_the_command_type_to_the_message_body
            _message.Body.Value.Should().Contain("\"type\":\"CM_STARTALL");
            //_should_serialize_the_message_type_to_the_header
            _message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            //_should_serialize_the_connection_name_to_the_message_body
            _message.Body.Value.Should().Contain("\"subscriptionName\":\"getallthethings\"");
            //_should_serialize_the_message_id_to_the_message_body
            _message.Body.Value.Should().Contain($"\"id\":\"{_command.Id}\"");
        }
    }
}
