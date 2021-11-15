using Greetings.Adaptors.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MsSql.EntityFrameworkCore;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter;
using Greetings.Ports.Events;
using Greetings.Ports.Mappers;

var builder = WebApplication.CreateBuilder(args);

string dbConnString = "server=(localdb)\\mssqllocaldb;database=BrighterTests;trusted_connection=yes";

//EF
builder.Services.AddDbContext<GreetingsDataContext>(o =>
{
    o.UseSqlServer(dbConnString);
});

//Brighter
string asbConnectionString = "Endpoint=sb://.servicebus.windows.net/;Authentication=Managed Identity";

var asbConnection = new AzureServiceBusConfiguration(asbConnectionString, true);
var producer = AzureServiceBusMessageProducerFactory.Get(asbConnection);

var outboxConfig = new MsSqlConfiguration(dbConnString, "BrighterOutbox");

builder.Services
    .AddBrighter(opt =>
    {
        opt.PolicyRegistry = new DefaultPolicy();
        opt.CommandProcessorLifetime = ServiceLifetime.Scoped;
    })
    .UseExternalBus(producer)
    .UseMsSqlOutbox(outboxConfig, typeof(MsSqlSqlAuthConnectionProvider))
    .UseMsSqlTransactionConnectionProvider(typeof(MsSqlEntityFrameworkCoreConnectionProvider<GreetingsDataContext>))
    .MapperRegistry(r =>
    {
        r.Add(typeof(GreetingEvent), typeof(GreetingEventMessageMapper));
        r.Add(typeof(GreetingAsyncEvent), typeof(GreetingEventAsyncMessageMapper));
    });


builder.Services.AddControllersWithViews();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    using var serviceScope = app.Services.CreateScope();
    var services = serviceScope.ServiceProvider;
    var dbContext = services.GetService<GreetingsDataContext>();

    dbContext.Database.EnsureCreated();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

app.Run();
