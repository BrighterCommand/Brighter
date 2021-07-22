using System.Text.Json;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public static class SnsMessageAttributeExtensions
    {
        public static string GetValueInString(this JsonElement jsonElement)
        {
            return jsonElement.TryGetProperty("Value", out var stringValue) ? stringValue.GetString() : string.Empty;
        }
    }
}
