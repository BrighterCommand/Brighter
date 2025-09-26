using System.Data.Common;
using Greetings.Adaptors.Data;
using Greetings.Adaptors.Services;
using Greetings.Ports.Commands;
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
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

var builder = WebApplication.CreateBuilder(args);

string dbConnString =
    "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password1!;Application Name=BrighterTests;MultipleActiveResultSets=True";

//EF
builder.Services.AddDbContext<GreetingsDataContext>(o =>
{
    o.UseSqlServer(dbConnString);
    //o.AddInterceptors(new AzureAdAuthenticationDbConnectionInterceptor());
});

//Services

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

//Brighter
string asbEndpoint = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

var asbConnection = new ServiceBusConnectionStringClientProvider(asbEndpoint);

var outboxConfig = new RelationalDatabaseConfiguration(dbConnString, 
    databaseName: "BrighterTests", outBoxTableName: "BrighterOutbox");

var producerRegistry = new AzureServiceBusProducerRegistryFactory(
        asbConnection,
        new AzureServiceBusPublication[]
        {
            new() { Topic = new RoutingKey("greeting.event"), MakeChannels = OnMissingChannel.Assume},
            new() { Topic = new RoutingKey("greeting.addGreetingCommand"), MakeChannels = OnMissingChannel.Assume },
            new() { Topic = new RoutingKey("greeting.Asyncevent"), MakeChannels = OnMissingChannel.Assume }
        }
    )
    .Create();

builder.Services
    .AddBrighter(opt =>
    {
        opt.PolicyRegistry = new DefaultPolicy();
    })
    .MapperRegistry(r =>
    {
        r.Add(typeof(GreetingEvent), typeof(GreetingEventMessageMapper));
        r.Add(typeof(GreetingAsyncEvent), typeof(GreetingEventAsyncMessageMapper));
        r.Add(typeof(AddGreetingCommand), typeof(AddGreetingMessageMapper));
    })
    .AddProducers((configure) =>
    {
        configure.ProducerRegistry = producerRegistry;
        configure.Outbox = new MsSqlOutbox(outboxConfig);
        configure.TransactionProvider = typeof(MsSqlEntityFrameworkCoreTransactionProvider<GreetingsDataContext>);
    });


builder.Services.AddControllersWithViews();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    using var serviceScope = app.Services.CreateScope();
    var services = serviceScope.ServiceProvider;
    var dbContext = services.GetService<GreetingsDataContext>();

    //dbContext.Database.EnsureCreated();
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
