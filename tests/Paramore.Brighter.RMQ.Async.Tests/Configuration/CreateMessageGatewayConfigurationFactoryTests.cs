﻿#region Licence
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

public class CreateMessageGatewayConfigurationFactoryTests
{
    [Fact]
    public void When_creating_gateway_configuration_from_brighter_section_should_return_configured_connection()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test-connection",
                ["Brighter:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://guest:guest@localhost:5672/%2f",
                ["Brighter:RabbitMQ:Connection:AmpqUri:ConnectionRetryCount"] = "5",
                ["Brighter:RabbitMQ:Connection:AmpqUri:RetryWaitInMilliseconds"] = "1000",
                ["Brighter:RabbitMQ:Connection:AmpqUri:CircuitBreakTimeInMilliseconds"] = "60000",
                ["Brighter:RabbitMQ:Connection:Heartbeat"] = "30",
                ["Brighter:RabbitMQ:Connection:PersistMessages"] = "true",
                ["Brighter:RabbitMQ:Connection:ContinuationTimeout"] = "25",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        Assert.NotNull(gatewayConfiguration);
        Assert.IsType<RmqMessagingGatewayConnection>(gatewayConfiguration);
        
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.Equal("test-connection", rmqConnection.Name);
        Assert.NotNull(rmqConnection.AmpqUri);
        Assert.Equal("amqp://guest:guest@localhost:5672/%2f", rmqConnection.AmpqUri!.Uri.ToString());
        Assert.Equal(5, rmqConnection.AmpqUri.ConnectionRetryCount);
        Assert.Equal(1000, rmqConnection.AmpqUri.RetryWaitInMilliseconds);
        Assert.Equal(60000, rmqConnection.AmpqUri.CircuitBreakTimeInMilliseconds);
        Assert.Equal((ushort)30, rmqConnection.Heartbeat);
        Assert.True(rmqConnection.PersistMessages);
        Assert.Equal((ushort)25, rmqConnection.ContinuationTimeout);
    }

    [Fact]
    public void When_creating_gateway_configuration_with_exchange_settings_should_configure_exchange()
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
                ["Brighter:RabbitMQ:Connection:Exchange:SupportDelay"] = "true",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.NotNull(rmqConnection.Exchange);
        Assert.Equal("my.exchange", rmqConnection.Exchange!.Name);
        Assert.Equal("topic", rmqConnection.Exchange.Type);
        Assert.True(rmqConnection.Exchange.Durable);
        Assert.True(rmqConnection.Exchange.SupportDelay);
    }

    [Fact]
    public void When_creating_gateway_configuration_with_dead_letter_exchange_should_configure_dlx()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test",
                ["Brighter:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://guest:guest@localhost:5672/",
                ["Brighter:RabbitMQ:Connection:DeadLetterExchange:Name"] = "my.dlx",
                ["Brighter:RabbitMQ:Connection:DeadLetterExchange:Type"] = "direct",
                ["Brighter:RabbitMQ:Connection:DeadLetterExchange:Durable"] = "true",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.NotNull(rmqConnection.DeadLetterExchange);
        Assert.Equal("my.dlx", rmqConnection.DeadLetterExchange!.Name);
        Assert.Equal("direct", rmqConnection.DeadLetterExchange.Type);
        Assert.True(rmqConnection.DeadLetterExchange.Durable);
    }

    [Fact]
    public void When_creating_gateway_configuration_with_named_instance_should_override_base_configuration()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Base configuration
                ["Brighter:RabbitMQ:Connection:Name"] = "base-connection",
                ["Brighter:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://guest:guest@localhost:5672/",
                ["Brighter:RabbitMQ:Connection:Heartbeat"] = "20",
                
                // Named instance configuration (should override)
                ["Brighter:RabbitMQ:analytics:Connection:Name"] = "analytics-connection",
                ["Brighter:RabbitMQ:analytics:Connection:AmpqUri:Uri"] = "amqp://user:pass@analytics-host:5672/analytics",
                ["Brighter:RabbitMQ:analytics:Connection:Heartbeat"] = "40",
                ["Brighter:RabbitMQ:analytics:Connection:PersistMessages"] = "true",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            "analytics", 
            null);

        //Assert
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.Equal("analytics-connection", rmqConnection.Name);
        Assert.Equal("amqp://user:pass@analytics-host:5672/analytics", rmqConnection.AmpqUri!.Uri.ToString());
        Assert.Equal((ushort)40, rmqConnection.Heartbeat);
        Assert.True(rmqConnection.PersistMessages);
    }

    [Fact]
    public void When_creating_gateway_configuration_from_aspire_section_should_use_aspire_connection_string()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test",
                ["Brighter:RabbitMQ:Connection:Heartbeat"] = "20",
                
                // Aspire configuration (should override URI)
                ["Aspire:RabbitMQ:Client:ConnectionString"] = "amqp://aspire:aspire@aspire-host:5672/",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.Equal("test", rmqConnection.Name);
        Assert.NotNull(rmqConnection.AmpqUri);
        Assert.Equal("amqp://aspire:aspire@aspire-host:5672/", rmqConnection.AmpqUri!.Uri.ToString());
        Assert.Equal((ushort)20, rmqConnection.Heartbeat);
    }

    [Fact]
    public void When_creating_gateway_configuration_from_named_aspire_section_should_use_named_aspire_connection()
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
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            "reporting", 
            null);

        //Assert
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.NotNull(rmqConnection.AmpqUri);
        Assert.Equal("amqp://reporting:reporting@reporting-host:5672/", rmqConnection.AmpqUri!.Uri.ToString());
    }

    [Fact]
    public void When_creating_gateway_configuration_from_connection_string_should_use_connection_string()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brighter:RabbitMQ:Connection:Name"] = "test",
                ["Brighter:RabbitMQ:Connection:Heartbeat"] = "20",
                
                // Connection string
                ["ConnectionStrings:messaging"] = "amqp://connstr:connstr@connstr-host:5672/vhost",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            "messaging", 
            null);

        //Assert
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.NotNull(rmqConnection.AmpqUri);
        Assert.Equal("amqp://connstr:connstr@connstr-host:5672/vhost", rmqConnection.AmpqUri!.Uri.ToString());
    }

    [Fact]
    public void When_creating_gateway_configuration_with_custom_section_name_should_use_custom_section()
    {
        //Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CustomMessaging:RabbitMQ:Connection:Name"] = "custom-connection",
                ["CustomMessaging:RabbitMQ:Connection:AmpqUri:Uri"] = "amqp://custom:custom@custom-host:5672/",
                ["CustomMessaging:RabbitMQ:Connection:Heartbeat"] = "50",
            })
            .Build();

        var factory = new RmqMessagingGatewayFromConfigurationFactory();
        var brighterConfig = new MicrosoftConfiguration(configuration);

        //Act
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            null, 
            "CustomMessaging:RabbitMQ");

        //Assert
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.Equal("custom-connection", rmqConnection.Name);
        Assert.Equal("amqp://custom:custom@custom-host:5672/", rmqConnection.AmpqUri!.Uri.ToString());
        Assert.Equal((ushort)50, rmqConnection.Heartbeat);
    }

    [Fact]
    public void When_creating_gateway_configuration_with_precedence_connection_string_should_win()
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
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            "prod", 
            null);

        //Assert
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.Equal("amqp://connstr:connstr@connstr-host:5672/winner", rmqConnection.AmpqUri!.Uri.ToString());
    }

    [Fact]
    public void When_creating_gateway_configuration_with_minimal_settings_should_use_defaults()
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
        var gatewayConfiguration = factory.CreateMessageGatewayConfigurationFactory(
            brighterConfig, 
            null, 
            null);

        //Assert
        var rmqConnection = (RmqMessagingGatewayConnection)gatewayConfiguration;
        Assert.NotNull(rmqConnection.AmpqUri);
        Assert.Equal(3, rmqConnection.AmpqUri!.ConnectionRetryCount); // Default
        Assert.Equal(1000, rmqConnection.AmpqUri.RetryWaitInMilliseconds); // Default
        Assert.Equal(60000, rmqConnection.AmpqUri.CircuitBreakTimeInMilliseconds); // Default
        Assert.Equal((ushort)20, rmqConnection.Heartbeat); // Default
        Assert.False(rmqConnection.PersistMessages); // Default
        Assert.Equal((ushort)20, rmqConnection.ContinuationTimeout); // Default
    }
}


