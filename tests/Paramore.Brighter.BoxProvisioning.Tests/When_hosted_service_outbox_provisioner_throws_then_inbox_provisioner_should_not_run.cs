#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// The hosted service stops at the first provisioner that fails: an inbox registered ahead of a
/// failing outbox is still ordered after it, and never runs.
/// </summary>
public class BoxProvisioningHostedServiceFailureShortCircuitTests
{
    [Fact]
    public async Task When_hosted_service_outbox_provisioner_throws_then_inbox_provisioner_should_not_run()
    {
        //Arrange
        var outboxFailure = new InvalidOperationException("simulated outbox migration failure");
        var inbox = new StubBoxProvisioner(BoxType.Inbox, "MyInbox");
        var outbox = new StubBoxProvisioner(BoxType.Outbox, "MyOutbox", throwOnProvision: outboxFailure);
        var logger = new CapturingLogger<BoxProvisioningHostedService>();
        var service = new BoxProvisioningHostedService(new IAmABoxProvisioner[] { inbox, outbox }, logger);

        //Act
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.StartAsync(default));

        //Assert
        Assert.True(outbox.ProvisionCalled, "outbox should have run first per OrderingOrdinal");
        Assert.False(inbox.ProvisionCalled, "inbox must not run after an earlier provisioner has thrown");
        Assert.Same(outboxFailure, ex.InnerException);
    }
}
