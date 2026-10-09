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
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection(JsonSerialisationCollection.NAME)]
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_configuring_json_serialisation_after_first_use_should_preserve_options_and_apply_configuration(bool serializeFirst)
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
            Assert.True(previousOptions.IsReadOnly);
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
        Assert.Same(builder, returnedBuilder);
        Assert.True(configuredOptions.WriteIndented);
        Assert.True(configuredOptions.PropertyNameCaseInsensitive);
        Assert.Same(JsonNamingPolicy.SnakeCaseLower, configuredOptions.PropertyNamingPolicy);
        Assert.Equal(previousConverters.Length + 1, configuredOptions.Converters.Count);
        Assert.Equal(previousConverters, configuredOptions.Converters.Take(previousConverters.Length));
        Assert.Same(converter, configuredOptions.Converters.Last());

        Assert.False(previousOptions.WriteIndented);
        Assert.Same(JsonNamingPolicy.CamelCase, previousOptions.PropertyNamingPolicy);
        Assert.Equal(previousConverters, previousOptions.Converters);

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("order-42", document.RootElement.GetProperty("order_id").GetString());
        Assert.Contains("\n", json);
    }

    [Fact]
    public void When_configuring_json_serialisation_again_after_use_should_preserve_the_first_configuration()
    {
        // Arrange
        var firstServices = new ServiceCollection();
        IBrighterBuilder firstBuilder = firstServices.AddBrighter();
        firstBuilder.ConfigureJsonSerialisation(options => options.WriteIndented = true);
        JsonSerializerOptions firstOptions = JsonSerialisationOptions.Options;
        JsonConverter[] firstConverters = [.. firstOptions.Converters];
        _ = JsonSerializer.Serialize(new { OrderId = new Id("order-42") }, firstOptions);
        Assert.True(firstOptions.IsReadOnly);

        var secondServices = new ServiceCollection();
        IBrighterBuilder secondBuilder = secondServices.AddBrighter();

        // Act
        secondBuilder.ConfigureJsonSerialisation(options => options.PropertyNameCaseInsensitive = false);
        JsonSerializerOptions secondOptions = JsonSerialisationOptions.Options;

        // Assert
        Assert.True(secondOptions.WriteIndented);
        Assert.False(secondOptions.PropertyNameCaseInsensitive);
        Assert.Same(firstOptions.PropertyNamingPolicy, secondOptions.PropertyNamingPolicy);
        Assert.Equal(firstConverters, secondOptions.Converters);
        Assert.True(firstOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void When_json_serialisation_configuration_throws_should_leave_current_options_unchanged()
    {
        // Arrange
        JsonSerializerOptions previousOptions = JsonSerialisationOptions.Options;
        JsonConverter[] previousConverters = [.. previousOptions.Converters];
        var failure = new InvalidOperationException("Configuration failed");
        var services = new ServiceCollection();
        IBrighterBuilder builder = services.AddBrighter();

        // Act
        Exception? exception = Record.Exception(() => builder.ConfigureJsonSerialisation(options =>
        {
            options.WriteIndented = true;
            options.Converters.Clear();
            throw failure;
        }));

        // Assert
        Assert.Same(failure, exception);
        Assert.Same(previousOptions, JsonSerialisationOptions.Options);
        Assert.False(previousOptions.WriteIndented);
        Assert.Equal(previousConverters, previousOptions.Converters);
    }
}
