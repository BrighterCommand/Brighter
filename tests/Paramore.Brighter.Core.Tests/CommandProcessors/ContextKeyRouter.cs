using System;

namespace Paramore.Brighter.Core.Tests.CommandProcessors;

public class ContextKeyRouter(string key) : IAmARequestRouter
{
    public string Key { get; } = key;
}