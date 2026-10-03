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

using Azure.Identity;
using Paramore.Brighter.Transformers.Azure;
using Xunit;
using Assert = Xunit.Assert;

namespace Paramore.Brighter.Azure.Tests.Transformers.Configuration;

public class AzureBlobLuggageIncompleteConfigurationTests
{
    [Xunit.Theory]
    [InlineData(null, null, false, false)]
    [InlineData(null, "", false, false)]
    [InlineData("", null, false, false)]
    [InlineData("", "", false, false)]
    [InlineData("UseDevelopmentStorage=true", null, false, false)]
    [InlineData("UseDevelopmentStorage=true", "", false, false)]
    [InlineData(null, "luggage", false, false)]
    [InlineData("", "luggage", false, false)]
    [InlineData(null, null, true, false)]
    [InlineData(null, null, false, true)]
    public void When_configuring_blob_luggage_with_incomplete_options_should_explain_the_supported_pairs(
        string? connectionString, string? containerName, bool hasContainerUri, bool hasCredential)
    {
        // Arrange
        var options = new AzureBlobLuggageOptions
        {
            ConnectionString = connectionString,
            ContainerName = containerName,
            ContainerUri = hasContainerUri ? new Uri("https://storage.example/luggage") : null,
            Credential = hasCredential ? new AzureCliCredential() : null
        };

        // Act
        var exception = Record.Exception(() => new AzureBlobLuggageStore(options));

        // Assert
        var argumentException = Assert.IsType<ArgumentException>(exception);
        Assert.Equal("options", argumentException.ParamName);
        Assert.Contains(nameof(AzureBlobLuggageOptions.ContainerUri), argumentException.Message);
        Assert.Contains(nameof(AzureBlobLuggageOptions.Credential), argumentException.Message);
        Assert.Contains(nameof(AzureBlobLuggageOptions.ConnectionString), argumentException.Message);
        Assert.Contains(nameof(AzureBlobLuggageOptions.ContainerName), argumentException.Message);
    }
}
