using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.Tests;

public class AssemblyResolutionMissingDependenciesTests
{
    [Test]
    public async Task When_we_auto_register_handlers_from_assemblies_with_missing_dependencies()
    {
        //arrange
        var factory =
            new DefaultServiceProviderFactory(new ServiceProviderOptions
            {
                ValidateOnBuild = true, ValidateScopes = true
            });

        var services = new ServiceCollection();

        services.AddConsumers().AutoFromAssemblies();
        
        //act
        bool caught = false;
        try
        {
            var provider = factory.CreateServiceProvider(services);
        }
        catch (AggregateException ae)
        {
            await Assert.That(ae.Message).Contains("Some services are not able");
            caught = true;
        }
        
        //assert
        await Assert.That(caught).IsTrue();
        
    }
}
