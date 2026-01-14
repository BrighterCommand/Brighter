using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using GreetingsWeb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Paramore.Brighter.MemoryLeak.Tests.Infrastructure;

/// <summary>
/// Test server which runs the GreetingsWeb API sample.
/// I uses WebApplicationFactory to start the API
/// Provides in-process testing with TestServer for fast, deterministic tests.
/// </summary>
public class WebApiTestServer : WebApplicationFactory<Startup>, IDisposable
{
    private const string ENVIRONMENT_NAME = "ASPNETCORE_ENVIRONMENT";
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ENVIRONMENT_NAME);

        // Configure test-specific settings
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add in-memory configuration for tests
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = ENVIRONMENT_NAME,
                // Use RabbitMQ for messaging (should be running via Docker)
                ["BRIGHTER_TRANSPORT"] = "RabbitMQ",

                // Use SQLite in-memory database for fast tests
                ["DATABASE_TYPE_ENV"] = "Sqlite",

            });
        });
    }

    /// <summary>
    /// Sends a request to create a new person.
    /// </summary>
    /// <param name="name">The person's name</param>
    /// <returns>HTTP response from the API</returns>
    public async Task<HttpResponseMessage> CreatePersonAsync(string name)
    {
        var client = CreateClient();
        var content = new StringContent(
            $"{{\"name\":\"{name}\"}}",
            Encoding.UTF8,
            "application/json"
        );

        return await client.PostAsync("/api/people", content);
    }

    /// <summary>
    /// Sends a request to add a greeting for a person.
    /// This will trigger the full command pipeline including event publishing to the outbox.
    /// </summary>
    /// <param name="personName">The person to greet</param>
    /// <param name="greetingText">The greeting message</param>
    /// <returns>HTTP response from the API</returns>
    public async Task<HttpResponseMessage> SendGreetingAsync(string personName, string greetingText)
    {
        var client = CreateClient();
        var content = new StringContent(
            $"{{\"greeting\":\"{greetingText}\"}}",
            Encoding.UTF8,
            "application/json"
        );

        return await client.PostAsync($"/api/people/{personName}/greetings", content);
    }

    /// <summary>
    /// Sends a request to get greetings for a person.
    /// This exercises the Darker query pipeline.
    /// </summary>
    /// <param name="personName">The person's name</param>
    /// <returns>HTTP response from the API</returns>
    public async Task<HttpResponseMessage> GetGreetingsAsync(string personName)
    {
        var client = CreateClient();
        return await client.GetAsync($"/api/people/{personName}/greetings");
    }

    /// <summary>
    /// Sends a request to find a person by name.
    /// This exercises the Darker query pipeline.
    /// </summary>
    /// <param name="name">The person's name</param>
    /// <returns>HTTP response from the API</returns>
    public async Task<HttpResponseMessage> FindPersonAsync(string name)
    {
        var client = CreateClient();
        return await client.GetAsync($"/api/people/{name}");
    }

    /// <summary>
    /// Sends a request to delete a person.
    /// This exercises the command pipeline.
    /// </summary>
    /// <param name="name">The person's name</param>
    /// <returns>HTTP response from the API</returns>
    public async Task<HttpResponseMessage> DeletePersonAsync(string name)
    {
        var client = CreateClient();
        return await client.DeleteAsync($"/api/people/{name}");
    }

    /// <summary>
    /// Disposes the test server and its resources.
    /// </summary>
    public new void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
