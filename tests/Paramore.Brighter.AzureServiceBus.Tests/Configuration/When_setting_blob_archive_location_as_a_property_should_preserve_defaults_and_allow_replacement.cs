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

using System;
using Azure.Storage.Blobs.Models;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.Storage.Azure;

namespace Paramore.Brighter.AzureServiceBus.Tests.Configuration;

public class AzureArchiveLocationConfigurationTests
{
    [Test]
    public async Task When_setting_blob_archive_location_as_a_property_should_preserve_defaults_and_allow_replacement()
    {
        //Arrange
        var options = new AzureBlobArchiveProviderOptions(
            new Uri("https://configuration.example.test/archive"), new InMemoryConfigurationTokenCredential(),
            AccessTier.Cool, tagBlobs: false);
        var message = new Message(new MessageHeader(new Id("order-123"), new RoutingKey("orders"), MessageType.MT_EVENT),
            new MessageBody("order placed"));
        var property = typeof(AzureBlobArchiveProviderOptions).GetProperty(nameof(options.StorageLocationFunc));
        await Assert.That(property).IsNotNull();
        var defaultLocation = await Assert.That(property.GetValue(options)).IsTypeOf<Func<Message, string>>();
        await Assert.That(defaultLocation(message)).IsEqualTo("order-123");
        Func<Message, string> replacement = item => $"custom/{item.Id}";

        //Act
        property.SetValue(options, replacement);

        //Assert
        await Assert.That(options.StorageLocationFunc).IsSameReferenceAs(replacement);
        await Assert.That(options.StorageLocationFunc(message)).IsEqualTo("custom/order-123");
        options.StorageLocationFunc = item => $"changed/{item.Id}";
        await Assert.That(options.StorageLocationFunc(message)).IsEqualTo("changed/order-123");
    }
}
