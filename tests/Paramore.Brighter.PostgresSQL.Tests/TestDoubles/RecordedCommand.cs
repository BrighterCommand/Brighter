namespace Paramore.Brighter.PostgresSQL.Tests.TestDoubles;

public readonly record struct RecordedCommand(string CommandText, int CommandTimeout);
