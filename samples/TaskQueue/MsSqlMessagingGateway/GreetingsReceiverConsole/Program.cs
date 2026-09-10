#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using Events;
using Events.Ports.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = SampleDatabase.ConnectionString(
    builder.Configuration.GetConnectionString("Brighter"));

// The queue table is not provisioned by anything in Brighter — see QueueTableProvisioner.
// Running it here as well as in the sender is what lets either process be started first.
QueueTableProvisioner.EnsureQueueTable(connectionString, SampleDatabase.QueueTable);

// The two tables this process uses: the queue it reads, and the Inbox it de-duplicates
// against. No outBoxTableName, because this process has no Outbox.
var configuration = new RelationalDatabaseConfiguration(
    connectionString,
    inboxTableName: SampleDatabase.InboxTable,
    queueStoreTable: SampleDatabase.QueueTable);

builder.Services.AddConsumers(options =>
{
    options.Subscriptions =
    [
        // MsSqlSubscription, not Subscription — the MSSQL ChannelFactory downcasts.
        new MsSqlSubscription<GreetingEvent>(
            new SubscriptionName("paramore.example.greeting"),
            new ChannelName(SampleDatabase.GreetingTopic),
            new RoutingKey(SampleDatabase.GreetingTopic),
            timeOut: TimeSpan.FromMilliseconds(200),
            messagePumpType: MessagePumpType.Reactor)
    ];
    options.DefaultChannelFactory = new ChannelFactory(
        new MsSqlMessageConsumerFactory(configuration)
    );

    // Supplies the Inbox store. The policy lives on GreetingEventHandler's [UseInbox].
    options.InboxConfiguration = new InboxConfiguration(new MsSqlInbox(configuration));
})
// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
// Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
.UseScheduler(new InMemorySchedulerFactory())
// Registered BEFORE AddHostedService<ServiceActivatorHostedService>() below, and that order is
// load-bearing: hosted services start in registration order, so reversing these two starts the
// pump against an InboxMessages table that does not exist yet.
.UseBoxProvisioning(options => options.AddMsSqlInbox(configuration))
.AutoFromAssemblies()
// Runs the consumer validation specs at startup, including PumpHandlerMatch — the rule the
// subscription comment above cites. Without this the specs are registered and never executed,
// and a Proactor/sync mismatch is a runtime pump failure rather than a named startup error.
// ValidatePipelines extends IBrighterBuilder, so it chains here rather than off IServiceCollection.
.ValidatePipelines();

builder.Services.AddHostedService<ServiceActivatorHostedService>();

var host = builder.Build();
await host.RunAsync();
