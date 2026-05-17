#region Licence

/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.JsonConverters;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class When_configuring_json_serialisation
{
    [Fact]
    public void Should_preserve_existing_options()
    {
        // Arrange
        var converterCountBefore = JsonSerialisationOptions.Options.Converters.Count;
        var caseInsensitiveBefore = JsonSerialisationOptions.Options.PropertyNameCaseInsensitive;
        var writeIndentedBefore =  JsonSerialisationOptions.Options.WriteIndented;

        var services = new ServiceCollection();
        var builder = services.AddBrighter();

        // Act
        builder.ConfigureJsonSerialisation(options =>
        {
            options.WriteIndented = !writeIndentedBefore;
        });

        // Assert — the new setting was applied
        Assert.Equal(JsonSerialisationOptions.Options.WriteIndented, !writeIndentedBefore);

        // Assert — built-in converters were not wiped out
        Assert.Equal(converterCountBefore, JsonSerialisationOptions.Options.Converters.Count);

        // Assert — previously configured settings are still intact
        Assert.Equal(caseInsensitiveBefore, JsonSerialisationOptions.Options.PropertyNameCaseInsensitive);
    }
}
