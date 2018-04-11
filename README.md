# Paramore.Brighter.AspNetCore
ASP.NET Core integration for [Brighter](https://github.com/BrighterCommand/Paramore.Brighter).

[![Build status](https://ci.appveyor.com/api/projects/status/09ed8f3g2olmebna?svg=true)](https://ci.appveyor.com/project/dstockhammer/paramore-brighter-aspnetcore)
[![NuGet](https://img.shields.io/nuget/v/Paramore.Brighter.AspNetCore.svg)](https://www.nuget.org/packages/Paramore.Brighter.AspNetCore)

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
