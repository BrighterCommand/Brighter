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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// When provisioning multiple box tables (across schemas, multiple inboxes, multiple
/// outboxes), an operator reading the startup log needs to know which table the hosted
/// service is acting on — the BoxType alone collapses every outbox under one label and
/// every inbox under another, which is ambiguous when several are registered. The
/// hosted service must therefore include the box table name in each progress entry,
/// both on the success and the failure paths, and the failure-path
/// <see cref="ConfigurationException"/> message must also identify the offending table.
/// </summary>
public class BoxProvisioningHostedServiceLoggingTests
{
    [Test]
    public async Task When_provisioning_succeeds_start_and_success_log_entries_should_include_the_box_table_name()
    {
        //Arrange
        var provisioner = new StubBoxProvisioner(BoxType.Outbox, "OrdersOutbox");
        var logger = new CapturingLogger<BoxProvisioningHostedService>();
        var service = new BoxProvisioningHostedService(new[] { provisioner }, logger);

        //Act
        await service.StartAsync(default);

        //Assert
        await Assert.That((logger.Entries).Any(e => e.Level == LogLevel.Information && e.Message.Contains("OrdersOutbox"))).IsTrue();

        // Both the start and success entries must name the table — two Information entries
        // mention the table name, distinguishing concurrent outbox/inbox provisioning steps.
        var informationalMentions = logger.Entries
            .Count(e => e.Level == LogLevel.Information && e.Message.Contains("OrdersOutbox"));
        await Assert.That(informationalMentions).IsEqualTo(2);
    }

    [Test]
    public async Task When_provisioning_fails_error_log_entry_should_include_the_box_table_name()
    {
        //Arrange
        var underlying = new InvalidOperationException("forced failure for spec 0027 Item K test");
        var provisioner = new StubBoxProvisioner(BoxType.Inbox, "PaymentsInbox", throwOnProvision: underlying);
        var logger = new CapturingLogger<BoxProvisioningHostedService>();
        var service = new BoxProvisioningHostedService(new[] { provisioner }, logger);

        //Act
        await Assert.ThrowsAsync<ConfigurationException>(() => service.StartAsync(default));

        //Assert
        await Assert.That(logger.Entries.Any(e => e.Level == LogLevel.Error
                 && e.Message.Contains("PaymentsInbox")
                 && e.Exception == underlying)).IsTrue();
    }

    [Test]
    public async Task When_provisioning_fails_configuration_exception_message_should_include_the_box_table_name()
    {
        //Arrange
        var underlying = new InvalidOperationException("forced failure for spec 0027 Item K test");
        var provisioner = new StubBoxProvisioner(BoxType.Inbox, "PaymentsInbox", throwOnProvision: underlying);
        var logger = new CapturingLogger<BoxProvisioningHostedService>();
        var service = new BoxProvisioningHostedService(new[] { provisioner }, logger);

        //Act
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => service.StartAsync(default));

        //Assert
        await Assert.That(ex.Message).Contains("PaymentsInbox");
        await Assert.That(ex.InnerException).IsSameReferenceAs(underlying);
    }
}
