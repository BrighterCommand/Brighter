namespace Paramore.Brighter.MSSQL.Tests.TestDoubles;

public readonly record struct RecordedCommand(string CommandText, int CommandTimeout);
