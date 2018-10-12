# .NET Core Extensions for [Brighter](https://github.com/BrighterCommand/Paramore.Brighter).

 - Dependency Injection integration for Brighter
 - Dependency Injection integration for Service Activator
 - IHostedService for background tasks 

[![Build status](https://ci.appveyor.com/api/projects/status/gw8l6btumifwfye7/branch/master?svg=true)](https://ci.appveyor.com/project/BrighterCommand/paramore-brighter-extensions/branch/master)

[![NuGet](https://img.shields.io/nuget/v/Paramore.Brighter.Extensions.DependencyInjection.svg)](https://www.nuget.org/packages/Paramore.Brighter.Extensions.DependencyInjection)

# 1. Paramore.Brighter.Extensions.DependencyInjection

## Usage
In your `ConfigureServices` method, use `AddBrighter` to add Brighter to the container.

```csharp
// This method gets called by the runtime. Use this method to add services to the container.
public void ConfigureServices(IServiceCollection services)
{
    // Add Brighter.
    services.AddBrighter()
        .AsyncHandlersFromAssemblies(typeof(CreateFooHandler).Assembly);

    // Add framework services.
    services.AddMvc();
}
```

You can customize Brighter by configuring `BrighterOptions`:

```csharp
// This method gets called by the runtime. Use this method to add services to the container.
public void ConfigureServices(IServiceCollection services)
{
    // Add Brighter.
    services.AddBrighter(opts =>
        {
            opts.RequestContextFactory = new MyCustomRequestContextFactory();
            opts.PolicyRegistry = new MyCustomPolicies();
            opts.MessagingConfiguration = new MyTaskQueues();
        })
    .AsyncHandlersFromAssemblies(typeof(CreateFooHandler).Assembly);

    // Add framework services.
    services.AddMvc();
}
```
# 2. Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection

## Usage
In your `ConfigureServices` method, use `AddServiceActivator` to add Service Activator to the container.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddServiceActivator(options =>
        {
            options.Connections = connections;
            options.ChannelFactory = new InputChannelFactory(rmqMessageConsumerFactory);
        })
        .MapperRegistryFromAssemblies(typeof(GreetingEventHandler).Assembly)
        .HandlersFromAssemblies(typeof(GreetingEventHandler).Assembly);
}
```

# 3. Paramore.Brighter.ServiceActivator.Extensions.Hosting

Extension to easly implement background tasks and scheduled jobs using `IHostedService` see
[Implement background tasks](https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/multi-container-microservice-net-applications/background-tasks-with-ihostedservice)

## Usage
In your `ConfigureServices` method, use `AddHostedService<ServiceActivatorHostedService>()` to add ServiceActivatorHostedService to the container.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddHostedService<ServiceActivatorHostedService>();
}
```
