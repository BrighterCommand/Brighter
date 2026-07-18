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

using System;
using System.Linq;
using Paramore.Brighter.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning.Tests;

public class UseBoxProvisioningPublicApiTests
{
    // Pre-spec-0027 the extension exposed two ways to set the migration lock timeout:
    // a `TimeSpan? migrationLockTimeout` parameter on UseBoxProvisioning AND
    // BoxProvisioningOptions.MigrationLockTimeout assignable from the configure delegate.
    // The dual surface had a real ordering bug — the parameter was applied to options
    // BEFORE the configure delegate ran, so a delegate that called AddMsSqlOutbox(...) then
    // set opts.MigrationLockTimeout would silently lose the assignment (the backend
    // extension captures the timeout at registration time). The fix consolidates on the
    // delegate path: the parameter is removed so MigrationLockTimeout is set exclusively
    // through BoxProvisioningOptions, with the documented requirement that callers set
    // it BEFORE invoking AddXxxOutbox/AddXxxInbox in the same delegate. Source-breaking
    // on netstandard2.0 (no DIM workaround); rolled into the existing Breaking Change PR.
    [Test]
    public async Task When_use_box_provisioning_is_inspected_it_should_have_exactly_one_overload()
    {
        //Arrange + Act
        var overloads = typeof(BrighterBuilderBoxProvisioningExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(BrighterBuilderBoxProvisioningExtensions.UseBoxProvisioning))
            .ToArray();

        //Assert
        await Assert.That(overloads).HasSingleItem();
    }

    [Test]
    public async Task When_use_box_provisioning_parameters_are_inspected_they_should_be_builder_and_configure_only()
    {
        //Arrange + Act
        var method = typeof(BrighterBuilderBoxProvisioningExtensions)
            .GetMethods()
            .Single(m => m.Name == nameof(BrighterBuilderBoxProvisioningExtensions.UseBoxProvisioning));
        var parameterTypes = method.GetParameters()
            .Select(p => p.ParameterType)
            .ToArray();

        //Assert
        await Assert.That(parameterTypes).IsEquivalentTo(
            new[] { typeof(IBrighterBuilder), typeof(Action<BoxProvisioningOptions>) },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task When_use_box_provisioning_parameters_are_inspected_none_should_be_named_migration_lock_timeout()
    {
        //Arrange + Act
        var method = typeof(BrighterBuilderBoxProvisioningExtensions)
            .GetMethods()
            .Single(m => m.Name == nameof(BrighterBuilderBoxProvisioningExtensions.UseBoxProvisioning));
        var parameterNames = method.GetParameters()
            .Select(p => p.Name)
            .ToArray();

        //Assert
        await Assert.That(parameterNames).DoesNotContain("migrationLockTimeout");
    }
}
