namespace EfPilot.Core.Diagnostics;

public sealed class DiagnosticsFinding
{
    public required string Category { get; init; }

    public required DiagnosticsSeverity Severity { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public string? Path { get; init; }

    public string? Suggestion { get; init; }
}
