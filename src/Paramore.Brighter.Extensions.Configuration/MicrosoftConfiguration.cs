using Microsoft.Extensions.Configuration;

namespace Paramore.Brighter.Extensions.Configuration;

public class MicrosoftConfiguration(IConfiguration configuration) : IAmAConfiguration
{
    public IAmAConfiguration GetSection(string sectionName)
    {
        return new MicrosoftConfiguration(configuration.GetSection(sectionName));
    }

    public T? Get<T>()
    {
        return configuration.Get<T>();
    }

    public string? GetConnectionString(string name)
    {
        return configuration.GetConnectionString(name);
    }

    public void Bind<T>(T obj)
    {
        configuration.Bind(obj);
    }
}
