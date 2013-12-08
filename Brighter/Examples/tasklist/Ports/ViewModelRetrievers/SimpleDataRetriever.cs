using System.IO;
using System.Reflection;

namespace Tasklist.Ports.ViewModelRetrievers
{
    public class SimpleDataRetriever
    {
        private static readonly string databasePath; 

        static SimpleDataRetriever()
        {
            if (System.Web.HttpContext.Current != null)
            {
                databasePath = System.Web.HttpContext.Current.Server.MapPath("~\\App_Data\\Tasks.sdf");
            }
            else
            {
                databasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase.Substring(8)), "App_Data\\Tasks.sdf");
            }
        }

        protected static string DatabasePath
        {
            get { return databasePath; }
        }
    }
}