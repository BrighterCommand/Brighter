using System;
using Paramore.Brighter.RequestValidation.Attributes;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

/// <summary>A request whose handler pipeline declares a validation step — used to test the (B) provider check.</summary>
public class MyValidatedCommand : Command
{
    public MyValidatedCommand() : base(Guid.NewGuid()) { }
}

/// <summary>A public sync handler whose Handle declares a [ValidateRequest] step (needs a validation provider).</summary>
public class MyValidatedSyncHandler : RequestHandler<MyValidatedCommand>
{
    [ValidateRequest(step: 0)]
    public override MyValidatedCommand Handle(MyValidatedCommand command) => base.Handle(command);
}
