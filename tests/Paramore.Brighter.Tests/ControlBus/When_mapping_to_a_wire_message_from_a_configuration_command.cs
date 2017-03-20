#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using Xunit;
using Paramore.Brighter.ServiceActivator.Ports;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.Tests.ControlBus
{
    public class ConfigurationCommandToMessageMapperTests
    {
        private IAmAMessageMapper<ConfigurationCommand> _mapper;
        private Message _message;
        private ConfigurationCommand _command;

        public ConfigurationCommandToMessageMapperTests()
        {
            _mapper = new ConfigurationCommandMessageMapper();

            //"{\"Type\":1,\"ConnectionName\":\"getallthethings\",\"Id\":\"XXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX\"}"
            _command = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL) {ConnectionName = "getallthethings"};
        }


        [Fact]
        public void When_mapping_to_a_wire_message_from_a_configuration_command()
        {
            _message = _mapper.MapToMessage(_command);

            // _should_serialize_the_command_type_to_the_message_body
            Assert.True(_message.Body.Value.Contains("\"Type\":1"));
            //_should_serialize_the_message_type_to_the_header
            Assert.AreEqual(MessageType.MT_COMMAND, _message.Header.MessageType);
            //_should_serialize_the_connection_name_to_the_message_body
            Assert.True(_message.Body.Value.Contains("\"ConnectionName\":\"getallthethings\""));
            //_should_serialize_the_message_id_to_the_message_body
            Assert.True(_message.Body.Value.Contains(string.Format("\"Id\":\"{0}\"", _command.Id)));
        }
    }
}