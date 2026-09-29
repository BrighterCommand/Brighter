#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;


namespace Paramore.Brighter.Extensions.Tests;

public class ScannedEventHandlersTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async System.Threading.Tasks.Task When_scanning_assemblies_repeatedly_should_preserve_distinct_event_handlers(bool addConsumers)
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = addConsumers ? services.AddConsumers() : services.AddBrighter();
        builder.HandlersFromAssemblies([typeof(ScannedNotification).Assembly], null);
        builder.HandlersFromAssemblies([typeof(ScannedNotification).Assembly], null);
        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var notification = new ScannedNotification();

        //Act
        processor.Publish(notification);

        //Assert
        await Assert.That(notification.FirstHandlerCalls).IsEqualTo(1);
        await Assert.That(notification.SecondHandlerCalls).IsEqualTo(1);
    }
}
