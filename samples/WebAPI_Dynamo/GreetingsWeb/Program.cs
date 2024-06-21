using System.IO;
using GreetingsWeb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

CreateHostBuilder(args).Build().Run();
return;

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, configBuilder) =>
        {
            var env = context.HostingEnvironment;
            configBuilder.AddJsonFile("appsettings.json", optional: false);
            configBuilder.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);
            configBuilder.AddEnvironmentVariables(prefix: "BRIGHTER_");
        })
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseKestrel();
            webBuilder.UseContentRoot(Directory.GetCurrentDirectory());
            webBuilder.CaptureStartupErrors(true);
            webBuilder.UseSetting("detailedErrors", "true");
            webBuilder.ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConsole();
                logging.AddDebug();
            });
            webBuilder.UseDefaultServiceProvider((context, options) =>
            {
                var isDevelopment = context.HostingEnvironment.IsDevelopment();
                options.ValidateScopes = isDevelopment;
                options.ValidateOnBuild = isDevelopment;
            });
            webBuilder.UseStartup<Startup>();
        });
