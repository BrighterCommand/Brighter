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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.Tests.RelationalConfiguration;

public class NonRelationalConfigurationRegistrationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_configuring_non_relational_producers_should_not_register_database_configuration(bool explicitOutbox)
    {
        //Arrange
        var services = new ServiceCollection();
        var outbox = explicitOutbox ? new InMemoryOutbox(TimeProvider.System) : null;

        //Act
        services.AddBrighter().AddProducers(options => { options.Outbox = outbox; });
        using var provider = services.BuildServiceProvider();

        //Assert
        await Assert.That(services.Any(descriptor => descriptor.ServiceType == typeof(IAmARelationalDatabaseConfiguration))).IsFalse();
        await Assert.That(provider.GetService<IAmARelationalDatabaseConfiguration>()).IsNull();
        await Assert.That(provider.GetRequiredService<IAmAnOutbox>()).IsTypeOf<InMemoryOutbox>();
        await Assert.That(provider.GetRequiredService<IAmACommandProcessor>()).IsNotNull();
        if (explicitOutbox)
            await Assert.That(provider.GetRequiredService<IAmAnOutbox>()).IsSameReferenceAs(outbox);
    }
}
