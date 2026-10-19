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
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// The hosted service names the box table in each start, success and failure log entry, and in
/// the failure's <see cref="ConfigurationException"/>, because the <see cref="BoxType"/> alone is
/// ambiguous when several outboxes or inboxes are registered.
/// </summary>
public class BoxProvisioningHostedServiceLoggingTests
{
    [Fact]
    public async Task When_provisioning_succeeds_start_and_success_log_entries_should_include_the_box_table_name()
    {
        //Arrange
        var provisioner = new StubBoxProvisioner(BoxType.Outbox, "OrdersOutbox");
        var logger = new CapturingLogger<BoxProvisioningHostedService>();
        var service = new BoxProvisioningHostedService(new[] { provisioner }, logger);

        //Act
        await service.StartAsync(default);

        //Assert
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Information && e.Message.Contains("OrdersOutbox"));

        var informationalMentions = logger.Entries
            .Count(e => e.Level == LogLevel.Information && e.Message.Contains("OrdersOutbox"));
        Assert.Equal(2, informationalMentions);
    }

    [Fact]
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
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Error
                 && e.Message.Contains("PaymentsInbox")
                 && e.Exception == underlying);
    }

    [Fact]
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
        Assert.Contains("PaymentsInbox", ex.Message);
        Assert.Same(underlying, ex.InnerException);
    }
}
