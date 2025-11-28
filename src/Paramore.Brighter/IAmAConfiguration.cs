namespace Paramore.Brighter;

public interface IAmAConfiguration
{
    IAmAConfiguration GetSection(string sectionName);
    
    T? Get<T>();
    string? GetConnectionString(string name);

    void Bind<T>(T obj);
}

public interface IAmGatewayConfigurationFactoryFromConfiguration
{
    IAmGatewayConfiguration Create(IAmAConfiguration configuration, string? name, string? sectionName);
}

public interface IAmAMessageConsumerFactoryFromConfiguration
{
    IAmAMessageConsumerFactory Create(IAmAConfiguration configuration, string? connectionString, string? sectionName);
}

public interface IAmAMessageProducerFactoryFromConfiguration
{
    IAmAMessageProducerFactory Create(IAmAConfiguration configuration, string? connectionString, string? sectionName);
}

public interface IAmAProducerRegistryFactoryFromConfiguration
{
    IAmAProducerRegistryFactory Create(IAmAConfiguration configuration, string? connectionString, string? sectionName);
}


public interface IAmMessagingGatewayFactoryFromConfiguration
{
    IAmAChannelFactory CreateChannelFactory(IAmAConfiguration configuration, string name, string? sectionName);
    IAmAMessageConsumerFactory CreateMessageConsumerFactory(IAmAConfiguration configuration, string name, string? sectionName);
    IAmAProducerRegistryFactory CreateProducerRegistryFactory(IAmAConfiguration configuration, string name, string? sectionName);
}
