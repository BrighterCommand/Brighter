using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace DbMaker;

public static class ConnectionResolver
{
    public static IAmazonDynamoDB CreateAndRegisterClient(IServiceCollection services, bool isLocal)
    {
        var client = isLocal ? CreateAndRegisterLocalClient(services) : CreateAndRegisterRemoteClient(services);
        return client;
    }
    
    private static IAmazonDynamoDB CreateAndRegisterLocalClient(IServiceCollection services)
    {
        var credentials = new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey");
            
        var clientConfig = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };

        var dynamoDb = new AmazonDynamoDBClient(credentials, clientConfig);
        services.Add(new ServiceDescriptor(typeof(IAmazonDynamoDB), dynamoDb));

        return dynamoDb;
    }     
        
    private static IAmazonDynamoDB CreateAndRegisterRemoteClient(IServiceCollection services)
    {
        //you need to implement this if you want to use this with an AWS account
        throw new NotImplementedException();
    }
}
