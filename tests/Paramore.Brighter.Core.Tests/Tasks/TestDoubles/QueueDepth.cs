namespace Paramore.Brighter.Core.Tests.Tasks.TestDoubles;

/// <summary>One observed confirmation queue depth, with the attributes it was reported under.</summary>
public sealed record QueueDepth(string? MessagingSystem, string? Destination, long Value);
