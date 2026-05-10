using Orchestra.Application.McpServers;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Tests.Tests.McpServers;

/// <summary>
/// Unit tests for <see cref="McpToolDangerLevelClassifier.Classify"/>.
/// TDD Phase 2 — Red: all tests fail until Phase 3 implements the method.
/// </summary>
public sealed class McpToolDangerLevelClassifierTests
{
    // ── Safe classification ──────────────────────────────────────────────────

    [Fact]
    public void Classify_ToolWithNeutralName_ReturnsSafe()
    {
        var result = McpToolDangerLevelClassifier.Classify("get_file_content", "Reads a file");

        Assert.Equal(DangerLevel.Safe, result);
    }

    [Fact]
    public void Classify_NullDescription_DoesNotThrow_ReturnsSafe()
    {
        var result = McpToolDangerLevelClassifier.Classify("list_items", null);

        Assert.Equal(DangerLevel.Safe, result);
    }

    [Fact]
    public void Classify_EmptyDescription_ReturnsSafe()
    {
        var result = McpToolDangerLevelClassifier.Classify("fetch_user", "");

        Assert.Equal(DangerLevel.Safe, result);
    }

    // ── Moderate classification ──────────────────────────────────────────────

    [Theory]
    [InlineData("update_record", "Updates a record in the database")]
    [InlineData("modify_config", "Modifies configuration")]
    [InlineData("write_file", "Writes content to a file")]
    [InlineData("patch_order", "Patches an existing order")]
    [InlineData("edit_comment", "Edits a comment")]
    [InlineData("overwrite_settings", "Overwrites user settings")]
    [InlineData("replace_value", "Replaces the current value")]
    public void Classify_ToolWithModerateKeyword_ReturnsModerate(string name, string description)
    {
        var result = McpToolDangerLevelClassifier.Classify(name, description);

        Assert.Equal(DangerLevel.Moderate, result);
    }

    [Fact]
    public void Classify_ModerateKeywordInDescriptionOnly_ReturnsModerate()
    {
        var result = McpToolDangerLevelClassifier.Classify("apply_changes", "Will modify the target resource");

        Assert.Equal(DangerLevel.Moderate, result);
    }

    // ── Destructive classification ───────────────────────────────────────────

    [Theory]
    [InlineData("delete_record", "Permanently deletes a record")]
    [InlineData("drop_table", "Drops the specified table")]
    [InlineData("remove_user", "Removes a user from the system")]
    [InlineData("destroy_session", "Destroys the active session")]
    [InlineData("truncate_logs", "Truncates the log table")]
    [InlineData("wipe_cache", "Wipes all cached data")]
    [InlineData("purge_queue", "Purges the message queue")]
    [InlineData("erase_history", "Erases the user's history")]
    [InlineData("reset_database", "Resets the entire database")]
    [InlineData("terminate_process", "Terminates the running process")]
    public void Classify_ToolWithDestructiveKeyword_ReturnsDestructive(string name, string description)
    {
        var result = McpToolDangerLevelClassifier.Classify(name, description);

        Assert.Equal(DangerLevel.Destructive, result);
    }

    [Fact]
    public void Classify_DestructiveKeywordInDescriptionOnly_ReturnsDestructive()
    {
        var result = McpToolDangerLevelClassifier.Classify("clean_up", "This will delete all orphaned records");

        Assert.Equal(DangerLevel.Destructive, result);
    }

    // ── Priority: Destructive over Moderate ──────────────────────────────────

    [Fact]
    public void Classify_ToolMatchingBothDestructiveAndModerate_ReturnsDestructive()
    {
        // Name contains "update" (Moderate), description contains "delete" (Destructive)
        var result = McpToolDangerLevelClassifier.Classify(
            "update_and_delete_record",
            "Updates record then deletes stale versions");

        Assert.Equal(DangerLevel.Destructive, result);
    }

    // ── Case-insensitivity ───────────────────────────────────────────────────

    [Theory]
    [InlineData("DELETE_ALL", "Uppercase destructive")]
    [InlineData("Delete_All", "Mixed case")]
    [InlineData("WIPE_DATA", "All caps wipe")]
    public void Classify_KeywordIsCaseInsensitive_ReturnsDestructive(string name, string description)
    {
        var result = McpToolDangerLevelClassifier.Classify(name, description);

        Assert.Equal(DangerLevel.Destructive, result);
    }
}
