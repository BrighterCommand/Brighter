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
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.Locking.Azure;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.Configuration;

public class AzureLockLocationConfigurationTests
{
    [Fact]
    public void When_setting_blob_lock_location_as_a_property_should_preserve_defaults_and_allow_replacement()
    {
        //Arrange
        var options = new AzureBlobLockingProviderOptions(
            new Uri("https://configuration.example.test/locks"), new InMemoryConfigurationTokenCredential());
        var property = typeof(AzureBlobLockingProviderOptions).GetProperty(nameof(options.StorageLocationFunc));
        Assert.NotNull(property);
        var defaultLocation = Assert.IsType<Func<string, string>>(property.GetValue(options));
        Assert.Equal("lock-Orders", defaultLocation("Orders"));
        Func<string, string> replacement = resource => $"custom/{resource}";

        //Act
        property.SetValue(options, replacement);

        //Assert
        Assert.Same(replacement, options.StorageLocationFunc);
        Assert.Equal("custom/Orders", options.StorageLocationFunc("Orders"));
        options.StorageLocationFunc = resource => $"changed/{resource}";
        Assert.Equal("changed/Orders", options.StorageLocationFunc("Orders"));
    }
}
