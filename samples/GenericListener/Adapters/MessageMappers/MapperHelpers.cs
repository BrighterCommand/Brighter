using Newtonsoft.Json.Linq;

namespace GenericListener.Adapters.MessageMappers
{
    public static class MapperHelpers
    {
        public static bool HasProperty(this JObject jsonObject, string propertyName)
        {
            return jsonObject.Property(propertyName) != null;
        }

        public static bool HasProperty(this JToken jsonObject, string propertyName)
        {
            return ((JObject)jsonObject).Property(propertyName) != null;
        }
    }
}