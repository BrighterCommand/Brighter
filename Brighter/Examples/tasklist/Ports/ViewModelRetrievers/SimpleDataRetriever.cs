using System.IO;
using System.Reflection;

namespace Tasklist.Ports.ViewModelRetrievers
{
    public class SimpleDataRetriever
    {
        protected static readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");
    }
}