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

public class When_hosted_service_outbox_provisioner_throws_then_inbox_provisioner_should_not_run
{
    // BoxProvisioningHostedService.StartAsync orders provisioners outbox-first then iterates
    // with a foreach that re-raises after wrapping any non-cancellation failure in
    // ConfigurationException. The reviewer of PR #4039 flagged that no test pinned
    // OrderingOrdinal semantics under failure — i.e. that an inbox registered alongside a
    // throwing outbox does not run after the outbox fails. The behaviour exists today (a
    // throw exits the foreach before the next iteration), but without a test it could
    // silently regress if a future contributor "improves" StartAsync by swapping the
    // foreach for Task.WhenAll, a try/continue, or any aggregator that collects failures.

    [Fact]
    public async Task Should_short_circuit_after_outbox_failure_and_leave_inbox_provisioner_untouched()
    {
        //Arrange — register the inbox FIRST in the IEnumerable so that any failure to apply
        //          OrderingOrdinal would surface as "inbox ran before outbox could fail",
        //          and the short-circuit assertion would still catch the regression because
        //          inbox.ProvisionCalled would have been set.
        var outboxFailure = new InvalidOperationException("simulated outbox migration failure");
        var inbox = new StubBoxProvisioner(BoxType.Inbox, "MyInbox");
        var outbox = new StubBoxProvisioner(BoxType.Outbox, "MyOutbox", throwOnProvision: outboxFailure);
        var logger = new CapturingLogger<BoxProvisioningHostedService>();
        var service = new BoxProvisioningHostedService(new IAmABoxProvisioner[] { inbox, outbox }, logger);

        //Act
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.StartAsync(default));

        //Assert — outbox ran (and threw), inbox was never reached. The wrapped exception
        //         identifies which provisioner failed so an operator reading the startup log
        //         can find the offending box without grep-walking every registration.
        Assert.True(outbox.ProvisionCalled, "outbox should have run first per OrderingOrdinal");
        Assert.False(inbox.ProvisionCalled, "inbox must not run after an earlier provisioner has thrown");
        Assert.Same(outboxFailure, ex.InnerException);
    }
}
