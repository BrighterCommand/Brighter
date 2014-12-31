using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

namespace paramore.brighter.commandprocessor.messaginggateway.restms.Parsers
{
    public static class XmlResultParser
    {
        public static bool TryParse<T>(string body, out T domainObject)
        {
            MemoryStream ms = null;
            var deserializer = new XmlSerializer(typeof(T));
            try
            {
                ms = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(body));
                domainObject = (T)deserializer.Deserialize(ms);
                return true;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
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
