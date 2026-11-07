namespace Paramore.Brighter.MySQL.Tests.TestDoubles;

public readonly record struct RecordedCommand(string CommandText, int CommandTimeout);
