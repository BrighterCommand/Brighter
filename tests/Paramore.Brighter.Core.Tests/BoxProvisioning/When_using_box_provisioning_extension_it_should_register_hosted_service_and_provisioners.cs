using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning;

public class When_using_box_provisioning_extension_it_should_register_hosted_service_and_provisioners
{
    [Fact]
    public void Should_register_hosted_service_and_provisioners()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new StubBrighterBuilder(services);

        //Act
        builder.UseBoxProvisioning(opts =>
        {
            opts.Add(svc => svc.AddSingleton<IAmABoxProvisioner>(new SpyBoxProvisioner(BoxType.Outbox)));
        });

        var provider = services.BuildServiceProvider();

        //Assert
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s is BoxProvisioningHostedService);

        var provisioners = provider.GetServices<IAmABoxProvisioner>().ToList();
        Assert.Single(provisioners);
        Assert.Equal(BoxType.Outbox, provisioners[0].BoxType);
    }
}
