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

#nullable enable

using System;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

public class When_hosted_service_is_started_with_a_provisioner_for_an_unrecognised_box_type_it_should_throw_argument_out_of_range
{
    // BoxProvisioningHostedService previously ordered provisioners with
    //     OrderBy(p => p.BoxType == BoxType.Outbox ? 0 : 1)
    // — works for the current { Outbox, Inbox } enum but degrades silently if a third
    // BoxType is added: a future Lockbox / DeadLetterBox would land in the "everything
    // else" bucket and provision in DI registration order, intermittent and unobservable.
    // The fix replaces the lambda with a switch over BoxType whose default arm throws
    // ArgumentOutOfRangeException — the next contributor adding an enum value gets a
    // loud startup failure that names the file and method to update, instead of a quiet
    // mis-ordering in production. This test pins that contract using (BoxType)999 as
    // the probe — a value safely outside the declared enum range that any future
    // BoxType addition will not collide with.

    [Fact]
    public async Task Should_throw_argument_out_of_range_naming_the_unknown_box_type_and_the_switch_method()
    {
        //Arrange — drive the default arm of the new OrderingOrdinal switch with a BoxType
        //          value that no current or future enum addition is expected to use.
        var unknownBoxType = (BoxType)999;
        var provisioner = new StubBoxProvisioner(unknownBoxType, "MysteryTable");
        var logger = new CapturingLogger<BoxProvisioningHostedService>();
        var service = new BoxProvisioningHostedService(new[] { provisioner }, logger);

        //Act
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.StartAsync(default));

        //Assert — the message names the unknown value (so the operator sees which BoxType
        //         tripped the guard) and the switch method (so the next contributor knows
        //         exactly where to add the new arm).
        Assert.Contains("999", ex.Message, StringComparison.Ordinal);
        Assert.Contains("OrderingOrdinal", ex.Message, StringComparison.Ordinal);
    }
}
