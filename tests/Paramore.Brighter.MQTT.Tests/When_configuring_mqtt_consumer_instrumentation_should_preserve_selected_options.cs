#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.MQTT.Tests;

[Trait("Category", "MQTT")]
public class MqttConsumerInstrumentationSelectionTests
{
    [Theory]
    [InlineData(InstrumentationOptions.All)]
    [InlineData(InstrumentationOptions.All & ~InstrumentationOptions.RequestBody)]
    [InlineData(InstrumentationOptions.None)]
    public void When_configuring_mqtt_consumer_instrumentation_should_preserve_selected_options(
        InstrumentationOptions instrumentationOptions)
    {
        //Arrange
        var configuration = new MqttMessagingGatewayConsumerConfiguration();

        //Act
        configuration.InstrumentationOptions = instrumentationOptions;

        //Assert
        Assert.Equal(instrumentationOptions, configuration.InstrumentationOptions);
    }
}
