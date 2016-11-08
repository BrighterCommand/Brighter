// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ianp
// Created          : 25-03-2014
//
// Last Modified By : ian
// Last Modified On : 25-03-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Nancy;
using Nancy.Conventions;
using Nancy.Testing;
using Nancy.TinyIoc;
using NUnit.Framework;
using NUnit.Specifications;
using nUnitShouldAdapter;
using Nancy.ViewEngines;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Modules;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors
{
    [Subject(typeof(IndexModule))]
    public class When_retrieving_home :  NUnit.Specifications.ContextSpecification
    {
        private Establish _context = () =>
        {
            var configurableBootstrapper = new TestConfigurableBootstrapper(with =>
            {
                with.Module<IndexModule>();
                with.ViewLocationProvider<ResourceViewLocationProvider>();
            });

            browser = new Browser(configurableBootstrapper);
        };

        private Because _with_GET = () => result = browser.Get("/", with =>
        {
            with.HttpRequest();
            with.Header("accept", "text/html");
        }).Result;

        private It should_return_200_OK = () => Assert.AreEqual(result.StatusCode, HttpStatusCode.OK);
        private It should_return_text_html = () => Assert.AreEqual(result.ContentType, "text/html");

        private static Browser browser;
        private static BrowserResponse result;
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