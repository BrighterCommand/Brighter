using System;
using FluentAssertions;
using Nancy;
using Nancy.Conventions;
using Nancy.Testing;
using Nancy.TinyIoc;
using Nancy.ViewEngines;
using Xunit;
using Paramore.Brighter.MessageViewer.Adaptors.API.Configuration;
using Paramore.Brighter.MessageViewer.Adaptors.API.Modules;

namespace Paramore.Brighter.Viewer.Tests.Adaptors.IndexModuleTests
{
    public class HomeEndpointTests
    {
        private Browser _browser;
        private BrowserResponse _result;

        public HomeEndpointTests()
        {
            var configurableBootstrapper = new TestConfigurableBootstrapper(with =>
            {
                with.Module<IndexModule>();
                with.ViewLocationProvider<ResourceViewLocationProvider>();
            });

            _browser = new Browser(configurableBootstrapper);
        }

        [Fact]
        public void When_retrieving_home()
        {
            _result = _browser.Get("/", with =>
                {
                    with.HttpRequest();
                    with.Header("accept", "text/html");
                })
                .Result;

            //should_return_200_OK
            HttpStatusCode.OK.Should().Be(_result.StatusCode);
            //should_return_text_html
            _result.ContentType.Should().Be("text/html");
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