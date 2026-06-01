using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Skills.DTOs;

namespace Orchestra.Infrastructure.Skills;

public class SkillFolderDiscoveryService : ISkillFolderDiscoveryService
{
    private readonly ILogger<SkillFolderDiscoveryService> _logger;

    public SkillFolderDiscoveryService(ILogger<SkillFolderDiscoveryService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<DiscoveredSkillDto>> DiscoverSkillsAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
            return Task.FromResult<IReadOnlyList<DiscoveredSkillDto>>([]);

        var discovered = new List<DiscoveredSkillDto>();

        foreach (var skillDir in Directory.EnumerateDirectories(folderPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillMdPath = Path.Combine(skillDir, "SKILL.md");
            if (!File.Exists(skillMdPath))
                continue;

            var skill = TryParseSkillMd(skillMdPath, skillDir);
            if (skill is not null)
                discovered.Add(skill);
        }

        return Task.FromResult<IReadOnlyList<DiscoveredSkillDto>>(discovered);
    }

    private DiscoveredSkillDto? TryParseSkillMd(string skillMdPath, string skillDir)
    {
        try
        {
            var content = File.ReadAllText(skillMdPath);
            return ParseFrontmatter(content, Path.GetFileName(skillDir));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping skill at '{Path}' due to read/parse error", skillMdPath);
            return null;
        }
    }

    private static DiscoveredSkillDto? ParseFrontmatter(string content, string dirName)
    {
        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("---"))
            return null;

        var firstDelimiter = trimmed.IndexOf("---", StringComparison.Ordinal);
        var secondDelimiter = trimmed.IndexOf("---", firstDelimiter + 3, StringComparison.Ordinal);
        if (secondDelimiter < 0)
            return null;

        var frontmatter = trimmed.Substring(firstDelimiter + 3, secondDelimiter - firstDelimiter - 3);

        var name = ExtractFrontmatterValue(frontmatter, "name") ?? dirName;
        var description = ExtractFrontmatterValue(frontmatter, "description") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
            return null;

        return new DiscoveredSkillDto(name.Trim(), description.Trim());
    }

    private static string? ExtractFrontmatterValue(string frontmatter, string key)
    {
        foreach (var line in frontmatter.Split('\n'))
        {
            var trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = trimmedLine.Substring(key.Length + 1).Trim();
            return value.Trim('"', '\'');
        }

        return null;
    }
}
