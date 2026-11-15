#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia <irakli.gabisonia94@gmail.com>

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
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.JsonConverters;
using System.Threading.Tasks;
namespace Paramore.Brighter.Extensions.Tests;

[NotInParallel(JsonSerialisationCollection.NAME)]
public class JsonSerialisationCopyOnConfigureTests : IDisposable
{
    private readonly JsonSerializerOptions _originalOptions = JsonSerialisationOptions.Options;

    public JsonSerialisationCopyOnConfigureTests()
    {
        JsonSerialisationOptions.Options = new JsonSerializerOptions(_originalOptions)
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public void Dispose() => JsonSerialisationOptions.Options = _originalOptions;

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task When_configuring_json_serialisation_after_first_use_should_preserve_options_and_apply_configuration(bool serializeFirst)
    {
        // Arrange
        JsonSerializerOptions previousOptions = JsonSerialisationOptions.Options;
        JsonConverter[] previousConverters = [.. previousOptions.Converters];
        var converter = new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower);
        var services = new ServiceCollection();
        IBrighterBuilder builder = services.AddBrighter();

        if (serializeFirst)
        {
            _ = JsonSerializer.Serialize(new { OrderId = new Id("order-42") }, previousOptions);
            await Assert.That(previousOptions.IsReadOnly).IsTrue();
        }

        // Act
        IBrighterBuilder returnedBuilder = builder.ConfigureJsonSerialisation(options =>
        {
            options.WriteIndented = true;
            options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.Converters.Add(converter);
        });
        JsonSerializerOptions configuredOptions = JsonSerialisationOptions.Options;
        string json = JsonSerializer.Serialize(new { OrderId = new Id("order-42") }, configuredOptions);

        // Assert
        await Assert.That(returnedBuilder).IsSameReferenceAs(builder);
        await Assert.That(configuredOptions.WriteIndented).IsTrue();
        await Assert.That(configuredOptions.PropertyNameCaseInsensitive).IsTrue();
        await Assert.That(configuredOptions.PropertyNamingPolicy).IsSameReferenceAs(JsonNamingPolicy.SnakeCaseLower);
        await Assert.That(configuredOptions.Converters.Count).IsEqualTo(previousConverters.Length + 1);
        await Assert.That(configuredOptions.Converters.Take(previousConverters.Length)).IsEqualTo(previousConverters);
        await Assert.That(configuredOptions.Converters.Last()).IsSameReferenceAs(converter);

        await Assert.That(previousOptions.WriteIndented).IsFalse();
        await Assert.That(previousOptions.PropertyNamingPolicy).IsSameReferenceAs(JsonNamingPolicy.CamelCase);
        await Assert.That(previousOptions.Converters).IsEqualTo(previousConverters);

        using JsonDocument document = JsonDocument.Parse(json);
        await Assert.That(document.RootElement.GetProperty("order_id").GetString()).IsEqualTo("order-42");
        await Assert.That(json).Contains("\n");
    }

    [Test]
    public async Task When_configuring_json_serialisation_again_after_use_should_preserve_the_first_configuration()
    {
        // Arrange
        var firstServices = new ServiceCollection();
        IBrighterBuilder firstBuilder = firstServices.AddBrighter();
        firstBuilder.ConfigureJsonSerialisation(options => options.WriteIndented = true);
        JsonSerializerOptions firstOptions = JsonSerialisationOptions.Options;
        JsonConverter[] firstConverters = [.. firstOptions.Converters];
        _ = JsonSerializer.Serialize(new { OrderId = new Id("order-42") }, firstOptions);
        await Assert.That(firstOptions.IsReadOnly).IsTrue();

        var secondServices = new ServiceCollection();
        IBrighterBuilder secondBuilder = secondServices.AddBrighter();

        // Act
        secondBuilder.ConfigureJsonSerialisation(options => options.PropertyNameCaseInsensitive = false);
        JsonSerializerOptions secondOptions = JsonSerialisationOptions.Options;

        // Assert
        await Assert.That(secondOptions.WriteIndented).IsTrue();
        await Assert.That(secondOptions.PropertyNameCaseInsensitive).IsFalse();
        await Assert.That(secondOptions.PropertyNamingPolicy).IsSameReferenceAs(firstOptions.PropertyNamingPolicy);
        await Assert.That(secondOptions.Converters).IsEqualTo(firstConverters);
        await Assert.That(firstOptions.PropertyNameCaseInsensitive).IsTrue();
    }

    [Test]
    public async Task When_json_serialisation_configuration_throws_should_leave_current_options_unchanged()
    {
        // Arrange
        JsonSerializerOptions previousOptions = JsonSerialisationOptions.Options;
        JsonConverter[] previousConverters = [.. previousOptions.Converters];
        var failure = new InvalidOperationException("Configuration failed");
        var services = new ServiceCollection();
        IBrighterBuilder builder = services.AddBrighter();

        // Act
        Exception? exception = null;
        try
        {
            builder.ConfigureJsonSerialisation(options =>
        {
            options.WriteIndented = true;
            options.Converters.Clear();
            throw failure;
        });
        }
        catch (Exception e) { exception = e; }

        // Assert
        await Assert.That(exception).IsSameReferenceAs(failure);
        await Assert.That(JsonSerialisationOptions.Options).IsSameReferenceAs(previousOptions);
        await Assert.That(previousOptions.WriteIndented).IsFalse();
        await Assert.That(previousOptions.Converters).IsEqualTo(previousConverters);
    }
}
