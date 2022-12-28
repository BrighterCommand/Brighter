using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Schemas;

public class SchemaRegistryTransformerTests 
{

    public SchemaRegistryTransformerTests()
    {
        var schemaRegistry = new InMemorySchemaRegistry();
        _schemaRegistryTransformer = new SchemaRegistryTransformer(schemaRegistry);
    }

    [Fact]
    public async Task When_registering_the_schema_for_a_message()
    {
        
    }
}
