using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Paramore.Brighter.MessagingGateway.MsSql.SqlQueues
{
    /// <inheritdoc />
    /// <summary>
    /// This class is used to deserialize an MsSql message to Message class with it's readonly properties
    /// </summary>
    public class MessageDefaultContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);

            if (!prop.Writable)
            {
                if (member is PropertyInfo property)
                {
                    var hasPrivateSetter = property.GetSetMethod(true) != null;
                    prop.Writable = hasPrivateSetter;
                }
            }

            return prop;
        }
    }
}
