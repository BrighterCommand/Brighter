using System;
using System.IO;
using System.Xml.Serialization;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;

namespace paramore.brighter.commandprocessor.messaginggateway.restms.Parsers
{
    static class ResultParser
    {
        public static bool TryParse<T>(string body, out T domainObject)
        {
            MemoryStream ms = null;
            var deserializer = new XmlSerializer(typeof(RestMSFeed));
            try
            {
                ms = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(body));
                domainObject = (T)deserializer.Deserialize(ms);
                return true;
            }
            catch (Exception e)
            {
                domainObject = default(T);
                return false;
            }
            finally
            {
                if (ms != null)
                    ms.Dispose();
            }
        }
    }
}
