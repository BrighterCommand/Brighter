using System;
using Nancy;
using Nancy.Conventions;
using Nancy.Testing;
using Nancy.TinyIoc;
using Nancy.ViewEngines;
using NUnit.Framework;
using Paramore.Brighter.MessageViewer.Adaptors.API.Configuration;
using Paramore.Brighter.MessageViewer.Adaptors.API.Modules;

namespace Paramore.Brighter.Viewer.Tests.Adaptors.IndexModuleTests
{
    [TestFixture]
    public class HomeEndpointTests
    {
        private Browser _browser;
        private BrowserResponse _result;

        [SetUp]
        public void Establish()
        {
            var configurableBootstrapper = new TestConfigurableBootstrapper(with =>
            {
                with.Module<IndexModule>();
                with.ViewLocationProvider<ResourceViewLocationProvider>();
            });

            _browser = new Browser(configurableBootstrapper);
        }

        [Test]
        public void When_retrieving_home()
        {
            _result = _browser.Get("/", with =>
                {
                    with.HttpRequest();
                    with.Header("accept", "text/html");
                })
                .Result;

            //should_return_200_OK
            Assert.AreEqual(_result.StatusCode, HttpStatusCode.OK);
            //should_return_text_html
            Assert.AreEqual(_result.ContentType, "text/html");
        }

   }

    public class TestConfigurableBootstrapper : ConfigurableBootstrapper
    {
        public TestConfigurableBootstrapper(Action<ConfigurableBootstrapperConfigurator> func) : base(func)
        {
        }

        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {
            base.ConfigureConventions(nancyConventions);
            BootstrapperEmbeddedHelper.RegisterStaticEmbedded(nancyConventions);
        }

        protected override void ConfigureApplicationContainer(TinyIoCContainer container)
        {
            base.ConfigureApplicationContainer(container);
            BootstrapperEmbeddedHelper.RegisterViewLocationEmbedded();
        }
    }
}