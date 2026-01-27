#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Linq;
using Paramore.Brighter.Inbox.Attributes;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Extensions;

using static Paramore.Brighter.Extensions.ReflectionExtensions;

public class ReflectionExtensionsTests
{
    private sealed class TestRequestHandlerAttribute(int step) :
        RequestHandlerAttribute(step)
    {
        public override Type GetHandlerType() => typeof(object);
    }

    private sealed class AnotherRequestHandlerAttribute(int step) :
        RequestHandlerAttribute(step)
    {
        public override Type GetHandlerType() => typeof(object);
    }

    private sealed class TestWrapWithAttribute(int step) :
        WrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(object);
    }

    private sealed class AnotherWrapWithAttribute(int step) :
        WrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(object);
    }

    private sealed class TestUnwrapWithAttribute(int step) :
        UnwrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(object);
    }

    private sealed class AnotherUnwrapWithAttribute(int step) :
        UnwrapWithAttribute(step)
    {
        public override Type GetHandlerType() => typeof(object);
    }

    private sealed class NoAttributesHandler
    {
        public void Handle() { }
    }

    private sealed class SingleRequestHandlerAttributeHandler
    {
        [TestRequestHandler(1)]
        public void Handle() { }
    }

    private sealed class MultipleRequestHandlerAttributesHandler
    {
        [TestRequestHandler(1)]
        [AnotherRequestHandler(2)]
        public void Handle() { }
    }

    private sealed class SingleWrapWithAttributeMapper
    {
        [TestWrapWith(1)]
        public void MapToMessage() { }
    }

    private sealed class MultipleWrapWithAttributesMapper
    {
        [TestWrapWith(1)]
        [AnotherWrapWith(2)]
        public void MapToMessage() { }
    }

    private sealed class SingleUnwrapWithAttributeMapper
    {
        [TestUnwrapWith(1)]
        public void MapToRequest() { }
    }

    private sealed class MultipleUnwrapWithAttributesMapper
    {
        [TestUnwrapWith(1)]
        [AnotherUnwrapWith(2)]
        public void MapToRequest() { }
    }

    private sealed class NoGlobalInboxHandler
    {
        [NoGlobalInbox]
        public void Handle() { }
    }

    private sealed class UseInboxHandler
    {
        [UseInbox(1)]
        public void Handle() { }
    }

    private sealed class UseInboxAsyncHandler
    {
        [UseInboxAsync(1)]
        public void Handle() { }
    }

    private sealed class MixedAttributesHandler
    {
        [TestRequestHandler(1)]
        [TestWrapWith(2)]
        [TestUnwrapWith(3)]
        [NoGlobalInbox]
        public void Handle() { }
    }

    [Fact]
    public void When_method_has_no_attributes_should_return_empty()
    {
        var method = typeof(NoAttributesHandler)
            .GetMethod(nameof(NoAttributesHandler.Handle))!;

        var result = method.GetOtherHandlersInPipeline();

        Assert.Empty(result);
    }

    [Fact]
    public void When_method_has_single_request_handler_attribute_should_return_it()
    {
        var method = typeof(SingleRequestHandlerAttributeHandler)
            .GetMethod(nameof(SingleRequestHandlerAttributeHandler.Handle))!;

        var result = method.GetOtherHandlersInPipeline().ToList();

        Assert.Single(result);
        Assert.IsType<TestRequestHandlerAttribute>(result[0]);
    }

    [Fact]
    public void When_method_has_multiple_request_handler_attributes_should_return_all()
    {
        var method = typeof(MultipleRequestHandlerAttributesHandler)
            .GetMethod(nameof(MultipleRequestHandlerAttributesHandler.Handle))!;

        var result = method.GetOtherHandlersInPipeline().ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a is TestRequestHandlerAttribute);
        Assert.Contains(result, a => a is AnotherRequestHandlerAttribute);
    }

    [Fact]
    public void When_method_has_non_request_handler_attributes_should_not_return_them()
    {
        var method = typeof(MixedAttributesHandler)
            .GetMethod(nameof(MixedAttributesHandler.Handle))!;

        var result = method.GetOtherHandlersInPipeline().ToList();

        Assert.Single(result);
        Assert.IsType<TestRequestHandlerAttribute>(result[0]);
    }

    [Fact]
    public void When_method_has_no_wrap_attributes_should_return_empty()
    {
        var method = typeof(NoAttributesHandler)
            .GetMethod(nameof(NoAttributesHandler.Handle))!;

        var result = method.GetOtherWrapsInPipeline();

        Assert.Empty(result);
    }

    [Fact]
    public void When_method_has_single_wrap_attribute_should_return_it()
    {
        var method = typeof(SingleWrapWithAttributeMapper)
            .GetMethod(nameof(SingleWrapWithAttributeMapper.MapToMessage))!;

        var result = method.GetOtherWrapsInPipeline().ToList();

        Assert.Single(result);
        Assert.IsType<TestWrapWithAttribute>(result[0]);
    }

    [Fact]
    public void When_method_has_multiple_wrap_attributes_should_return_all()
    {
        var method = typeof(MultipleWrapWithAttributesMapper)
            .GetMethod(nameof(MultipleWrapWithAttributesMapper.MapToMessage))!;

        var result = method.GetOtherWrapsInPipeline().ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a is TestWrapWithAttribute);
        Assert.Contains(result, a => a is AnotherWrapWithAttribute);
    }

    [Fact]
    public void When_method_has_non_wrap_attributes_should_not_return_them()
    {
        var method = typeof(MixedAttributesHandler)
            .GetMethod(nameof(MixedAttributesHandler.Handle))!;

        var result = method.GetOtherWrapsInPipeline().ToList();

        Assert.Single(result);
        Assert.IsType<TestWrapWithAttribute>(result[0]);
    }

    [Fact]
    public void When_method_has_no_unwrap_attributes_should_return_empty()
    {
        var method = typeof(NoAttributesHandler)
            .GetMethod(nameof(NoAttributesHandler.Handle))!;

        var result = method.GetOtherUnwrapsInPipeline();

        Assert.Empty(result);
    }

    [Fact]
    public void When_method_has_single_unwrap_attribute_should_return_it()
    {
        var method = typeof(SingleUnwrapWithAttributeMapper)
            .GetMethod(nameof(SingleUnwrapWithAttributeMapper.MapToRequest))!;

        var result = method.GetOtherUnwrapsInPipeline().ToList();

        Assert.Single(result);
        Assert.IsType<TestUnwrapWithAttribute>(result[0]);
    }

    [Fact]
    public void When_method_has_multiple_unwrap_attributes_should_return_all()
    {
        var method = typeof(MultipleUnwrapWithAttributesMapper)
            .GetMethod(nameof(MultipleUnwrapWithAttributesMapper.MapToRequest))!;

        var result = method.GetOtherUnwrapsInPipeline().ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a is TestUnwrapWithAttribute);
        Assert.Contains(result, a => a is AnotherUnwrapWithAttribute);
    }

    [Fact]
    public void When_method_has_non_unwrap_attributes_should_not_return_them()
    {
        var method = typeof(MixedAttributesHandler)
            .GetMethod(nameof(MixedAttributesHandler.Handle))!;

        var result = method.GetOtherUnwrapsInPipeline().ToList();

        Assert.Single(result);
        Assert.IsType<TestUnwrapWithAttribute>(result[0]);
    }

    [Fact]
    public void When_method_has_no_inbox_attribute_should_return_true()
    {
        var method = typeof(NoGlobalInboxHandler)
            .GetMethod(nameof(NoGlobalInboxHandler.Handle))!;

        var result = method.HasNoInboxAttributesInPipeline();

        Assert.True(result);
    }

    [Fact]
    public void When_method_has_no_attributes_should_return_false()
    {
        var method = typeof(NoAttributesHandler)
            .GetMethod(nameof(NoAttributesHandler.Handle))!;

        var result = method.HasNoInboxAttributesInPipeline();

        Assert.False(result);
    }

    [Fact]
    public void When_method_has_other_attributes_but_not_no_inbox_should_return_false()
    {
        var method = typeof(SingleRequestHandlerAttributeHandler)
            .GetMethod(nameof(SingleRequestHandlerAttributeHandler.Handle))!;

        var result = method.HasNoInboxAttributesInPipeline();

        Assert.False(result);
    }

    [Fact]
    public void When_method_has_mixed_attributes_including_no_inbox_should_return_true()
    {
        var method = typeof(MixedAttributesHandler)
            .GetMethod(nameof(MixedAttributesHandler.Handle))!;

        var result = method.HasNoInboxAttributesInPipeline();

        Assert.True(result);
    }

    [Fact]
    public void When_method_has_use_inbox_attribute_should_return_true()
    {
        var method = typeof(UseInboxHandler)
            .GetMethod(nameof(UseInboxHandler.Handle))!;

        var result = method.HasExistingUseInboxAttributesInPipeline();

        Assert.True(result);
    }

    [Fact]
    public void When_method_has_use_inbox_async_attribute_should_return_true()
    {
        var method = typeof(UseInboxAsyncHandler)
            .GetMethod(nameof(UseInboxAsyncHandler.Handle))!;

        var result = method.HasExistingUseInboxAttributesInPipeline();

        Assert.True(result);
    }

    [Fact]
    public void When_method_has_no_inbox_attributes_should_return_false()
    {
        var method = typeof(NoAttributesHandler)
            .GetMethod(nameof(NoAttributesHandler.Handle))!;

        var result = method.HasExistingUseInboxAttributesInPipeline();

        Assert.False(result);
    }

    [Fact]
    public void When_method_has_other_attributes_but_no_use_inbox_should_return_false()
    {
        var method = typeof(SingleRequestHandlerAttributeHandler)
            .GetMethod(nameof(SingleRequestHandlerAttributeHandler.Handle))!;

        var result = method.HasExistingUseInboxAttributesInPipeline();

        Assert.False(result);
    }

    [Fact]
    public void When_method_has_no_global_inbox_but_not_use_inbox_should_return_false()
    {
        var method = typeof(NoGlobalInboxHandler)
            .GetMethod(nameof(NoGlobalInboxHandler.Handle))!;

        var result = method.HasExistingUseInboxAttributesInPipeline();

        Assert.False(result);
    }
}