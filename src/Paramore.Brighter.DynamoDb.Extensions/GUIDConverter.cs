using System;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.DynamoDb.Extensions
{
    public class GUIDConverter : IPropertyConverter
    {
        public DynamoDBEntry ToEntry(object value)
        {
            var uuid = (Guid)value;
            if (uuid == null)
                throw new InvalidOperationException(
                    $"Supplied type was of type {value.GetType().Name} not Accounts.Application.CardDetails");

            var json = uuid.ToString();

            DynamoDBEntry entry = new Primitive
            {
                Type = DynamoDBEntryType.String,
                Value = json
            };

            return entry;
 
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            var primitive = entry as Primitive;
            if (primitive == null || !(primitive.Value is String) || string.IsNullOrEmpty((string) primitive.Value))
                throw new ArgumentOutOfRangeException();

            var value = Guid.Parse(primitive.AsString()); 
            return value;
 
        }
    }
}
