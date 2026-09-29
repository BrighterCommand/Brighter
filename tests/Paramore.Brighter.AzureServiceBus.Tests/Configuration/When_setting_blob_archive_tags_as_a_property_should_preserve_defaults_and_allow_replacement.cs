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
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using Azure.Storage.Blobs.Models;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.Storage.Azure;

namespace Paramore.Brighter.AzureServiceBus.Tests.Configuration;

public class AzureArchiveTagsConfigurationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_setting_blob_archive_tags_as_a_property_should_preserve_defaults_and_allow_replacement(bool missingOptionalHeaders)
    {
        //Arrange
        var options = new AzureBlobArchiveProviderOptions(
            new Uri("https://configuration.example.test/archive"), new InMemoryConfigurationTokenCredential(),
            AccessTier.Cool, tagBlobs: true);
        var timestamp = new DateTimeOffset(2026, 9, 28, 12, 30, 0, TimeSpan.Zero);
        var message = new Message(new MessageHeader(new Id("order-123"), new RoutingKey("orders"), MessageType.MT_EVENT,
            timeStamp: timestamp, correlationId: new Id("correlation-123"),
            contentType: new ContentType("application/json")), new MessageBody("order placed"));
        if (missingOptionalHeaders)
        {
            message.Header.CorrelationId = null!;
            message.Header.ContentType = null!;
        }

        var property = typeof(AzureBlobArchiveProviderOptions).GetProperty(nameof(options.TagsFunc));
        await Assert.That(property).IsNotNull();
        var defaultTags = await Assert.That(property.GetValue(options)).IsTypeOf<Func<Message, Dictionary<string, string?>>>();
        var tags = defaultTags(message);
        await Assert.That(tags.Count).IsEqualTo(5);
        await Assert.That(tags["topic"]).IsEqualTo("orders");
        await Assert.That(tags["correlationId"]).IsEqualTo(missingOptionalHeaders ? null : "correlation-123");
        await Assert.That(tags["message_type"]).IsEqualTo("MT_EVENT");
        await Assert.That(tags["timestamp"]).IsEqualTo(timestamp.ToString(CultureInfo.InvariantCulture));
        await Assert.That(tags["content_type"]).IsEqualTo(missingOptionalHeaders ? MediaTypeNames.Text.Plain : "application/json");
        var customTags = new Dictionary<string, string?> { ["custom"] = "value" };
        Func<Message, Dictionary<string, string?>> replacement = _ => customTags;

        //Act
        property.SetValue(options, replacement);

        //Assert
        await Assert.That(options.TagsFunc).IsSameReferenceAs(replacement);
        await Assert.That(options.TagsFunc(message)).IsSameReferenceAs(customTags);
        options.TagsFunc = item => new Dictionary<string, string?> { ["id"] = item.Id.Value };
        await Assert.That(options.TagsFunc(message)["id"]).IsEqualTo("order-123");
    }
}
