namespace SartainStudios.Client.Schema;

public sealed record BuildInfo(DateTime BuildDateUtc, string? CommitMessage);