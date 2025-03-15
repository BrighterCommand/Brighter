using GreetingsApp.Requests;
using GreetingsWeb;
using GreetingsWeb.Models;
using Paramore.Brighter;
using Paramore.Darker;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Configuration.AddEnvironmentVariables("BRIGHTER_");

// Add services to the container.
builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.ConfigureEfCore(builder.Configuration);
builder.Services.ConfigureBrighter(builder.Configuration);
builder.Services.ConfigureDarker();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/greetings/{name}", async (string name, IQueryProcessor queryProcessor) =>
    await queryProcessor.ExecuteAsync(new FindGreetingsForPerson(name))
        is { } greetings
        ? Results.Ok(greetings)
        : Results.NotFound());

app.MapPost("/greetings/new", async (string name, NewGreeting newGreeting, IAmACommandProcessor commandProcessor, IQueryProcessor queryProcessor) =>
{
    await commandProcessor.SendAsync(new AddGreeting(name, newGreeting.Greeting));

    var personsGreetings = await queryProcessor.ExecuteAsync(new FindGreetingsForPerson(name));

    return personsGreetings == null ? Results.NotFound() : Results.Ok(personsGreetings);
});


app.MapGet("/people/{name}", async (string name, IQueryProcessor queryProcessor) =>  
    await queryProcessor.ExecuteAsync(new FindPersonByName(name)) is { } personResult
    ? Results.Ok(personResult)
    : Results.NotFound());

app.MapDelete("/people/{name}", async (string name, IAmACommandProcessor commandProcessor) => 
    await commandProcessor.SendAsync(new DeletePerson(name)));

app.MapPost("/people/new", async (NewPerson newPerson, IAmACommandProcessor commandProcessor, IQueryProcessor queryProcessor) =>
{
    await commandProcessor.SendAsync(new AddPerson(newPerson.Name));

    var addedPerson = await queryProcessor.ExecuteAsync(new FindPersonByName(newPerson.Name));

    return addedPerson == null ? Results.NotFound() : Results.Ok(addedPerson);
});

app.Run();
