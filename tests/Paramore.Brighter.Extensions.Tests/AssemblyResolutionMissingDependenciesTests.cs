using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class AssemblyResolutionMissingDependenciesTests
{
    [Fact]
    public void When_we_auto_register_handlers_from_assemblies_with_missing_dependencies()
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
        var provider = factory.CreateServiceProvider(services);
        
        //assert
        //we will not get here, if we have a missing dependency
        Assert.True(true);
        
    }
}
