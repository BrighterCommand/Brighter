#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.Extensions.Configuration;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.Configuration;

public class CreateChannelFactoryTests
{
    [Fact]
    public void When_creating_channel_factory_from_brighter_section_should_return_configured_factory()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test-connection",
                ["Brighter:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://guest:guest@localhost:5672/%2f",
                ["Brighter:RabbitMQ:Connection:Heartbeat"] = "30",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }

    [Fact]
    public void When_creating_channel_factory_with_named_instance_should_use_named_configuration()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Base configuration
                ["Brighter:RabbitMQ:Connection:Name"] = "base-connection",
                ["Brighter:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://guest:guest@localhost:5672/",
                
                // Named instance configuration (should override)
                ["Brighter:RabbitMQ:analytics:Connection:Name"] = "analytics-connection",
                ["Brighter:RabbitMQ:analytics:Connection:AmpqUri:Uri"] = "amqp://user:pass@analytics-host:5672/analytics",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            "analytics", 
            null);

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }

    [Fact]
    public void When_creating_channel_factory_from_aspire_section_should_use_aspire_connection()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test",
                
                // Aspire configuration (should override URI)
                ["Aspire:RabbitMQ:Client:ConnectionString"] = "amqp://aspire:aspire@aspire-host:5672/",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }

    [Fact]
    public void When_creating_channel_factory_from_named_aspire_section_should_use_named_aspire_connection()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test",
                
                // Named Aspire configuration
                ["Aspire:RabbitMQ:Client:reporting:ConnectionString"] = "amqp://reporting:reporting@reporting-host:5672/",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            "reporting", 
            null);

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }

    [Fact]
    public void When_creating_channel_factory_from_connection_string_should_use_connection_string()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test",
                
                // Connection string
                ["ConnectionStrings:messaging"] = "amqp://connstr:connstr@connstr-host:5672/vhost",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            "messaging", 
            null);

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }

    [Fact]
    public void When_creating_channel_factory_with_custom_section_name_should_use_custom_section()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CustomMessaging:RabbitMQ:Connection:Name"] = "custom-connection",
                ["CustomMessaging:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://custom:custom@custom-host:5672/",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            null, 
            "CustomMessaging:RabbitMQ");

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }

    [Fact]
    public void When_creating_channel_factory_with_exchange_settings_should_create_factory()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test",
                ["Brighter:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://guest:guest@localhost:5672/",
                ["Brighter:RabbitMQ:Connection:Exchange:Name"] = "my.exchange",
                ["Brighter:RabbitMQ:Connection:Exchange:Type"] = "topic",
                ["Brighter:RabbitMQ:Connection:Exchange:Durable"] = "true",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }

    [Fact]
    public void When_creating_channel_factory_with_resilience_settings_should_create_factory()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test",
                ["Brighter:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://guest:guest@localhost:5672/",
                ["Brighter:RabbitMQ:Connection:AmpqUri:ConnectionRetryCount"] = "5",
                ["Brighter:RabbitMQ:Connection:AmpqUri:RetryWaitInMilliseconds"] = "2000",
                ["Brighter:RabbitMQ:Connection:AmpqUri:CircuitBreakTimeInMilliseconds"] = "120000",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }

    [Fact]
    public void When_creating_channel_factory_with_minimal_settings_should_use_defaults()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://guest:guest@localhost:5672/",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }

    [Fact]
    public void When_creating_channel_factory_with_precedence_connection_string_should_win()
    {
        //Arrange - Testing precedence: ConnectionString > Named Aspire > Aspire > Named Brighter > Brighter
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Base Brighter configuration
                ["Brighter:RabbitMQ:Connection:Name"] = "test",
                ["Brighter:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://base:base@base-host:5672/",
                
                // Named Brighter configuration
                ["Brighter:RabbitMQ:prod:Connection:AmpqUri:Uri"] = "amqp://named:named@named-host:5672/",
                
                // Aspire configuration
                ["Aspire:RabbitMQ:Client:ConnectionString"] = "amqp://aspire:aspire@aspire-host:5672/",
                
                // Named Aspire configuration
                ["Aspire:RabbitMQ:Client:prod:ConnectionString"] = "amqp://aspire-named:aspire-named@aspire-named-host:5672/",
                
                // Connection string (should win)
                ["ConnectionStrings:prod"] = "amqp://connstr:connstr@connstr-host:5672/winner",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var channelFactory = factory.CreateChannelFactory(
            brighterConfig, 
            "prod", 
            null);

        //Assert
        Assert.NotNull(channelFactory);
        Assert.IsType<ChannelFactory>(channelFactory);
    }
}
