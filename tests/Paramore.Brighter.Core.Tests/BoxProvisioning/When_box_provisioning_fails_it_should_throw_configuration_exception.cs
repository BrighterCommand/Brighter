using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning;

public class When_box_provisioning_fails_it_should_throw_configuration_exception
{
    [Fact]
    public async Task Should_wrap_in_configuration_exception()
    {
        //Arrange
        var innerException = new InvalidOperationException("Database unreachable");
        var failingProvisioner = new FailingBoxProvisioner(BoxType.Outbox, innerException);
        var provisioners = new List<IAmABoxProvisioner> { failingProvisioner };

        var hostedService = new BoxProvisioningHostedService(
            provisioners,
            NullLogger<BoxProvisioningHostedService>.Instance
        );

        //Act
        var exception = await Assert.ThrowsAsync<ConfigurationException>(
            () => hostedService.StartAsync(CancellationToken.None)
        );

        //Assert
        Assert.Same(innerException, exception.InnerException);
        Assert.Contains("Outbox", exception.Message);
    }
}
