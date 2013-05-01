using System;
using System.Web.Optimization;

[assembly: WebActivator.PostApplicationStartMethod(
    typeof(paramore.web.App_Start.DurandalConfig), "PreStart")]

namespace paramore.web.App_Start
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