using System.Collections.Specialized;

namespace paramore.brighter.restms.core.Model
{
    public class MessageHeaders
    {
        readonly NameValueCollection headers = new NameValueCollection();

        public void AddHeader(string name, string value)
        {
            headers.Add(name, value);
        }

        public string this[string name]
        {
            get { return headers[name]; }
        }
    }
}
