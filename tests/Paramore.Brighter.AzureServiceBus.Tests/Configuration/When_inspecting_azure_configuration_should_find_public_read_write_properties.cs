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
using System.Reflection;
using Paramore.Brighter.Locking.Azure;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.Storage.Azure;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.Configuration;

public class AzureConfigurationPropertyDiscoveryTests
{
    [Theory]
    [InlineData(typeof(AzureServiceBusSubscriptionConfiguration), nameof(AzureServiceBusSubscriptionConfiguration.SqlFilter), typeof(string))]
    [InlineData(typeof(AzureServiceBusSubscriptionConfiguration), nameof(AzureServiceBusSubscriptionConfiguration.UseServiceBusQueue), typeof(bool))]
    [InlineData(typeof(AzureServiceBusPublication), nameof(AzureServiceBusPublication.UseServiceBusQueue), typeof(bool))]
    [InlineData(typeof(AzureServiceBusPublication<Command>), nameof(AzureServiceBusPublication.UseServiceBusQueue), typeof(bool))]
    [InlineData(typeof(AzureBlobLockingProviderOptions), nameof(AzureBlobLockingProviderOptions.StorageLocationFunc), typeof(Func<string, string>))]
    [InlineData(typeof(AzureBlobArchiveProviderOptions), nameof(AzureBlobArchiveProviderOptions.StorageLocationFunc), typeof(Func<Message, string>))]
    [InlineData(typeof(AzureBlobArchiveProviderOptions), nameof(AzureBlobArchiveProviderOptions.TagsFunc), typeof(Func<Message, Dictionary<string, string>>))]
    public void When_inspecting_azure_configuration_should_find_public_read_write_properties(
        Type optionsType, string memberName, Type memberType)
    {
        //Arrange
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        //Act
        var property = optionsType.GetProperty(memberName, flags);

        //Assert
        Assert.NotNull(property);
        Assert.Equal(memberType, property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod.IsPublic);
        Assert.NotNull(property.SetMethod);
        Assert.True(property.SetMethod.IsPublic);
        Assert.Null(optionsType.GetField(memberName, flags));
    }
}
