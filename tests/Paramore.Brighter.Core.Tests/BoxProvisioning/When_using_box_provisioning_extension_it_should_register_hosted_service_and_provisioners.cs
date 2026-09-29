using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning;

public class When_using_box_provisioning_extension_it_should_register_hosted_service_and_provisioners
{
    [Test]
    public async Task Should_register_hosted_service_and_provisioners()
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
        await Assert.That((hostedServices).Any(s => s is BoxProvisioningHostedService)).IsTrue();

        var provisioners = provider.GetServices<IAmABoxProvisioner>().ToList();
        await Assert.That(provisioners).HasSingleItem();
        await Assert.That(provisioners[0].BoxType).IsEqualTo(BoxType.Outbox);
    }
}
