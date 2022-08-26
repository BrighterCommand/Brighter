using Amazon.DynamoDBv2.DataModel;

namespace SalutationEntities
{
    [DynamoDBTable("Salutations")]
    public class Salutation
    { 
        [DynamoDBHashKey]
        [DynamoDBProperty]
        public string Greeting { get; set; }
    }
}
