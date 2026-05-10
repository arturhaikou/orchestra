using Orchestra.Domain.Enums;

namespace Orchestra.Application.McpServers;

public static class McpToolDangerLevelClassifier
{
    private static readonly HashSet<string> DestructiveKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "delete", "drop", "remove", "destroy", "truncate", "wipe", "purge", "erase", "reset", "terminate"
    };

    private static readonly HashSet<string> ModerateKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "update", "modify", "write", "patch", "edit", "overwrite", "replace"
    };

    public static DangerLevel Classify(string toolName, string? description)
    {
        var combined = $"{toolName} {description ?? string.Empty}";

        if (DestructiveKeywords.Any(k => combined.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return DangerLevel.Destructive;

        if (ModerateKeywords.Any(k => combined.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return DangerLevel.Moderate;

        return DangerLevel.Safe;
    }
}
