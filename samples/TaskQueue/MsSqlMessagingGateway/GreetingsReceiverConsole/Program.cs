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
using Events.Ports.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// The tables this process uses: the queue the transport reads, and the Inbox it
// de-duplicates against. GreetingsSender builds the matching configuration for the queue and
// the Outbox — the connection string and the table names have to agree across the two files.
// There is no outBoxTableName here because this process has no Outbox.
// Set ConnectionStrings__Brighter in the environment to point this somewhere other than the
// SQLEXPRESS default. It must match whatever GreetingsSender is using.
// GetConnectionString returns "" — not null — when the key exists but is blank, which an
// environment variable makes easy, so test for whitespace rather than null.
var configured = builder.Configuration.GetConnectionString("Brighter");
var connectionString = string.IsNullOrWhiteSpace(configured)
    ? @"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;"
    : configured;

var configuration = new RelationalDatabaseConfiguration(
    connectionString,
    databaseName: "BrighterSqlQueue",
    inboxTableName: "InboxMessages",
    queueStoreTable: "QueueData");

builder.Services.AddConsumers(options =>
{
    options.Subscriptions =
    [
        // MsSqlSubscription, NOT Subscription: the MSSQL ChannelFactory casts what it is
        // given down to MsSqlSubscription and throws ConfigurationException when the cast
        // fails. A plain Subscription<T> compiles and then dies as the Dispatcher starts.
        new MsSqlSubscription<GreetingEvent>(
            new SubscriptionName("paramore.example.greeting"),
            new ChannelName("greeting.event"),
            new RoutingKey("greeting.event"),
            timeOut: TimeSpan.FromMilliseconds(200),
            messagePumpType: MessagePumpType.Reactor)
    ];
    options.DefaultChannelFactory = new ChannelFactory(
        new MsSqlMessageConsumerFactory(configuration)
    );

    // The Inbox instance the [UseInbox] attribute on GreetingEventHandler writes to. The
    // attribute carries the policy (context key, once-only, what a duplicate does); this
    // registration is only what supplies the store.
    options.InboxConfiguration = new InboxConfiguration(new MsSqlInbox(configuration));
})
// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
// Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
.UseScheduler(new InMemorySchedulerFactory())
// Each process provisions the box it owns, so the receiver can be started on its own. This
// registers a hosted service, and the ORDER matters: it must be registered before
// AddHostedService<ServiceActivatorHostedService>() below, or the pump starts consuming
// against an InboxMessages table that does not exist yet.
.UseBoxProvisioning(options => options.AddMsSqlInbox(configuration))
.AutoFromAssemblies();

builder.Services.AddHostedService<ServiceActivatorHostedService>();

var host = builder.Build();
await host.RunAsync();
