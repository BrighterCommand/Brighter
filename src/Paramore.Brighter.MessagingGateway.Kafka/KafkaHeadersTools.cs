using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    internal static class KafkaHeadersTools
    {
        public static bool TryGetLastBytesIgnoreCase(this Headers headers, string key, out byte[] lastHeader)
        {
            var header = headers.FirstOrDefault(h => h.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (header == null)
            {
                lastHeader = null;
                return false;
            }

            lastHeader = header.GetValueBytes();
            return true;
        }
    }
}
