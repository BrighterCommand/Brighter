using System.Web.Optimization;
using paramore.rewind.web;

[assembly: WebActivator.PostApplicationStartMethod(typeof(DurandalConfig), "PreStart")]

namespace paramore.rewind.web
{
    public static class DurandalConfig
    {
        public static void PreStart()
        {
            // Add your start logic here
            DurandalBundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}