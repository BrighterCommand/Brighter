using System;
using System.Text;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public static class StringTools
    {
        public static byte[] ToByteArray(this string str)
        {
            return Encoding.ASCII.GetBytes(str);
        }

        public static string FromByteArray(this byte[] bytes)
        {
            return Encoding.ASCII.GetString(bytes);
        }
    }
}
