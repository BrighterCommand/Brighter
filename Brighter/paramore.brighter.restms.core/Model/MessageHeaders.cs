using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace paramore.brighter.restms.core.Model
{
    public class MessageHeaders
    {
        readonly NameValueCollection headers = new NameValueCollection();

        public void AddHeader(string name, string value)
        {
            headers.Add(name, value);
        }

        public IEnumerable<Tuple<string, string>>  All
        {
            get 
            {
                return from string key in headers select new Tuple<string, string>(key, headers[key]);
            }
        }


        public string this[string name]
        {
            get { return headers[name]; }
        }
    }
}
