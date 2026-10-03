#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// HttpContextScopeProvider must decide from the affinity alone, before it ever reads the current
// HttpContext. Given an accessor that counts every HttpContext read, and a live HttpContext behind it,
// an AlwaysNew ask returns null and never reads HttpContext at all - proving the decline happens without
// consulting the accessor, not merely that the accessor happened to return something unusable. A
// JoinAmbient ask against the same accessor is the control: it does read HttpContext and returns the
// request's own scope, showing the accessor would have registered a read had the AlwaysNew path made one.
public class ProviderDeclinesAlwaysNewWithoutConsultingAccessorTests
{
    [Fact]
    public void When_the_ask_carries_always_new_the_provider_should_not_consult_the_accessor()
    {
        // Arrange
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        var accessor = new RecordingHttpContextAccessor(context);
        var provider = new HttpContextScopeProvider(accessor);

        // Act
        var scope = provider.GetAmbient(ScopeAffinity.AlwaysNew);

        // Assert
        Assert.Null(scope);
        Assert.Equal(0, accessor.HttpContextReadCount);
    }

    [Fact]
    public void When_the_ask_carries_join_ambient_the_provider_should_consult_the_accessor()
    {
        // Arrange
        var requestServices = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = requestServices };
        var accessor = new RecordingHttpContextAccessor(context);
        var provider = new HttpContextScopeProvider(accessor);

        // Act
        var scope = provider.GetAmbient(ScopeAffinity.JoinAmbient);

        // Assert
        var requestScope = Assert.IsType<HttpRequestScope>(scope);
        Assert.Same(requestServices, requestScope.Services);
        Assert.True(accessor.HttpContextReadCount > 0);
    }
}
