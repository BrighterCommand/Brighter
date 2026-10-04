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

public class AzureBlobLuggageConnectionStringTests
{
    [Xunit.Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void When_configuring_blob_luggage_with_a_connection_string_should_construct_the_store(
        bool hasContainerUri, bool hasCredential)
    {
        // Arrange
        var options = new AzureBlobLuggageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "luggage",
            ContainerUri = hasContainerUri ? new Uri("https://storage.example/luggage") : null,
            Credential = hasCredential ? new AzureCliCredential() : null
        };

        // Act
        var exception = Record.Exception(() => new AzureBlobLuggageStore(options));

        // Assert
        Assert.Null(exception);
    }
}
