using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Orders.Data;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter;
using Orders.Domain;
using Orders.Domain.Entities;
using Orders.Domain.Events;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

var builder = WebApplication.CreateBuilder(args);

string dbConnString =
    "Server=127.0.0.1,11433;Database=BrighterOrderTests;User Id=sa;Password=Password1!;Application Name=BrighterTests;MultipleActiveResultSets=True";


//Services

builder.Services.AddScoped<IUnitOfWork, SqlUnitOfWork>();
builder.Services.AddScoped<SqlUnitOfWork, SqlUnitOfWork>();

builder.Services.AddTransient<IOrderRepository, OrderRepository>();

//Brighter
string asbEndpoint = ".servicebus.windows.net";

var asbConnection = new ServiceBusVisualStudioCredentialClientProvider(asbEndpoint);

var outboxConfig = new RelationalDatabaseConfiguration(dbConnString, outBoxTableName: "BrighterOutbox");

builder.Services
    .AddBrighter(opt =>
    {
        opt.PolicyRegistry = new DefaultPolicy();
        opt.CommandProcessorLifetime = ServiceLifetime.Scoped;
    })
    .UseExternalBus(
        new AzureServiceBusProducerRegistryFactory(
                asbConnection,
                new AzureServiceBusPublication[] { new() { Topic = new RoutingKey(NewOrderVersionEvent.Topic) }, }
            )
            .Create()
    )
    .UseMsSqlOutbox(outboxConfig, typeof(MsSqlSqlAuthConnectionProvider))
    .UseMsSqlTransactionConnectionProvider(typeof(SqlConnectionProvider))
    .AutoFromAssemblies(Assembly.GetAssembly(typeof(NewOrderVersionEvent)));


builder.Services.AddControllersWithViews().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});;

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    using var serviceScope = app.Services.CreateScope();
    var services = serviceScope.ServiceProvider;

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
