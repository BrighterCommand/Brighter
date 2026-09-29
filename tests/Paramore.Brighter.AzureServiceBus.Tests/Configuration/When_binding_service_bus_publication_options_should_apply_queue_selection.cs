#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

#nullable enable

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.Configuration;

public class AzurePublicationConfigurationBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_binding_service_bus_publication_options_should_apply_queue_selection(bool genericPublication)
    {
        //Arrange
        AzureServiceBusPublication publication = genericPublication
            ? new AzureServiceBusPublication<Command>()
            : new AzureServiceBusPublication();
        Assert.False(publication.UseServiceBusQueue);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["UseServiceBusQueue"] = "true"
        }).Build();

        //Act
        configuration.Bind(publication);

        //Assert
        Assert.True(publication.UseServiceBusQueue);
        publication.UseServiceBusQueue = false;
        Assert.False(publication.UseServiceBusQueue);
    }
}
