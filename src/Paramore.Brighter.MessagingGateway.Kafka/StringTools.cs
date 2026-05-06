using System;
using System.Text;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public static class StringTools
    {
        public static byte[] ToByteArray(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public static string FromByteArray(this byte[]? bytes)
        {
            if (bytes is null) return string.Empty;
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
