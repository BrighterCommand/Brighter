using Amazon.DynamoDBv2.DataModel;

namespace SalutationApp.Entities
{
    [DynamoDBTable("Salutations")]
    public class Salutation
    { 
        [DynamoDBHashKey]
        [DynamoDBProperty]
        public string Greeting { get; set; }
    }
}
