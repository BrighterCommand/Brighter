using System;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.DynamoDb.V4;

public class GUIDConverter : IPropertyConverter
{
    public DynamoDBEntry ToEntry(object value)
    {
        var uuid = (Guid)value;
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