using Orders.Sweeper.Extensions;

const string HEALTH_PATH = "/health";

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddRouting();
builder.Services.AddHealthChecks()
    .AddBrighterOutbox();

builder.AddBrighter();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapHealthChecks(HEALTH_PATH);
});

app.Run();
